using FastEndpoints.Messaging.Remote;
using FastEndpoints.Messaging.Remote.Core;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

interface IEventSubscriber
{
    void Start(CallOptions opts);
}

sealed class EventSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider>
    : BaseCommandExecutor<string, TEvent>, ICommandExecutor, IEventSubscriber
    where TEvent : class, IEvent
    where TEventHandler : class, IEventHandler<TEvent>
    where TStorageRecord : class, IEventStorageRecord, new()
    where TStorageProvider : IEventSubscriberStorageProvider<TStorageRecord>
{
    static TStorageProvider? _storage;
    static bool _isInMemProvider;

    readonly SemaphoreSlim _sem = new(0);
    readonly ObjectFactory _handlerFactory;
    readonly IServiceProvider _serviceProvider;
    readonly SubscriberExceptionReceiver? _errorReceiver;
    readonly ILogger<EventSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider>>? _logger;
    readonly string _subscriberID;

    public EventSubscriber(ChannelBase channel, string clientIdentifier, IServiceProvider serviceProvider)
        : base(channel: channel, methodType: MethodType.ServerStreaming, endpointName: $"{typeof(TEvent).FullName}/sub")
    {
        _subscriberID = (Environment.MachineName + GetType().FullName + channel.Target + clientIdentifier).ToHash();
        _serviceProvider = serviceProvider;
        _storage ??= (TStorageProvider)ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, typeof(TStorageProvider));
        _isInMemProvider = _storage is InMemoryEventSubscriberStorage;
        EventSubscriberStorage<TStorageRecord, TStorageProvider>.Provider = _storage; //setup stale record purge task
        EventSubscriberStorage<TStorageRecord, TStorageProvider>.IsInMemProvider = _isInMemProvider;
        _handlerFactory = ActivatorUtilities.CreateFactory(typeof(TEventHandler), Type.EmptyTypes);
        _errorReceiver = _serviceProvider.GetService<SubscriberExceptionReceiver>();
        _logger = serviceProvider.GetRequiredService<ILogger<EventSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider>>>();
        _logger.SubscriberRegistered(_subscriberID, typeof(TEventHandler).FullName!, typeof(TEvent).FullName!);
    }

    public void Start(CallOptions opts)
    {
        _ = EventReceiverTask(_storage!, _sem, opts, Invoker, Method, _subscriberID, _logger, _errorReceiver);
        _ = EventExecutorTask(_storage!, _sem, opts, _subscriberID, _logger, _handlerFactory, _serviceProvider, _errorReceiver);
    }

    static async Task EventReceiverTask(TStorageProvider storage,
                                        SemaphoreSlim sem,
                                        CallOptions opts,
                                        CallInvoker invoker,
                                        Method<string, TEvent> method,
                                        string subscriberID,
                                        ILogger? logger,
                                        SubscriberExceptionReceiver? errors)
    {
        var call = invoker.AsyncServerStreamingCall(method, null, opts, subscriberID);
        var createErrorCount = 0;
        var receiveErrorCount = 0;

        while (!opts.CancellationToken.IsCancellationRequested)
        {
            try
            {
                while (await call.ResponseStream.MoveNext(opts.CancellationToken)) // actual network call happens on MoveNext()
                {
                    var record = new TStorageRecord
                    {
                        SubscriberID = subscriberID,
                        EventType = typeof(TEvent).FullName!,
                        ExpireOn = DateTime.UtcNow.AddHours(4)
                    };
                    record.SetEvent(call.ResponseStream.Current);

                    while (!opts.CancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            await storage.StoreEventAsync(record, opts.CancellationToken);
                            sem.Release();
                            createErrorCount = 0;

                            break;
                        }
                        catch (Exception ex)
                        {
                            createErrorCount++;
                            _ = errors?.OnStoreEventRecordError<TEvent>(record, createErrorCount, ex, opts.CancellationToken);
                            logger?.StoreEventError(subscriberID, typeof(TEvent).FullName!, ex.Message);
                            await Task.Delay(5000);
                        }
                    }
                    receiveErrorCount = 0;
                }
            }
            catch (Exception ex)
            {
                receiveErrorCount++;
                _ = errors?.OnEventReceiveError<TEvent>(subscriberID, receiveErrorCount, ex, opts.CancellationToken);
                logger?.StreamReceiveTrace(subscriberID, typeof(TEvent).FullName!, ex.Message);
                call.Dispose(); //the stream is most likely broken, so dispose it and initialize a new call
                await Task.Delay(5000);
                call = invoker.AsyncServerStreamingCall(method, null, opts, subscriberID);
            }
        }
    }

    static async Task EventExecutorTask(TStorageProvider storage,
                                        SemaphoreSlim sem,
                                        CallOptions opts,
                                        string subscriberID,
                                        ILogger? logger,
                                        ObjectFactory handlerFactory,
                                        IServiceProvider serviceProvider,
                                        SubscriberExceptionReceiver? errors)
    {
        var retrievalErrorCount = 0;
        var executionErrorCount = 0;
        var updateErrorCount = 0;

        while (!opts.CancellationToken.IsCancellationRequested)
        {
            IEnumerable<TStorageRecord> records;

            try
            {
                records = await storage.GetNextBatchAsync(
                              new()
                              {
                                  CancellationToken = opts.CancellationToken,
                                  Limit = 25,
                                  SubscriberID = subscriberID,
                                  Match = e => e.SubscriberID == subscriberID && !e.IsComplete && DateTime.UtcNow <= e.ExpireOn
                              });
                retrievalErrorCount = 0;
            }
            catch (Exception ex)
            {
                retrievalErrorCount++;
                _ = errors?.OnGetNextEventRecordError<TEvent>(subscriberID, retrievalErrorCount, ex, opts.CancellationToken);
                logger?.StorageGetNextBatchError(subscriberID, typeof(TEvent).FullName!, ex.Message);
                await Task.Delay(5000);

                continue;
            }

            if (records.Any())
            {
                foreach (var record in records)
                {
                    var handler = handlerFactory.GetEventHandlerOrCreateInstance<TEvent, TEventHandler>(serviceProvider);

                    try
                    {
                        await handler.HandleAsync(record.GetEvent<TEvent>(), opts.CancellationToken);
                        executionErrorCount = 0;
                    }
                    catch (Exception ex)
                    {
                        if (_isInMemProvider)
                        {
                            try
                            {
                                await storage.StoreEventAsync(record, opts.CancellationToken);
                            }
                            catch
                            {
                                //ignore and discard event when queue is full
                            }
                        }
                        executionErrorCount++;
                        _ = errors?.OnHandlerExecutionError<TEvent, TEventHandler>(record, executionErrorCount, ex, opts.CancellationToken);
                        logger?.HandlerExecutionCritical(typeof(TEvent).FullName!, ex.Message);
                        await Task.Delay(5000);

                        break; //don't process the rest of the batch
                    }

                    while (!_isInMemProvider && !opts.CancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            record.IsComplete = true;
                            await storage.MarkEventAsCompleteAsync(record, opts.CancellationToken);
                            updateErrorCount = 0;

                            break;
                        }
                        catch (Exception ex)
                        {
                            updateErrorCount++;
                            _ = errors?.OnMarkEventAsCompleteError<TEvent>(record, updateErrorCount, ex, opts.CancellationToken);
                            logger?.StorageMarkAsCompleteError(subscriberID, typeof(TEvent).FullName!, ex.Message);
                            await Task.Delay(5000);
                        }
                    }
                }
            }
            else
            {
                //wait until either the semaphore is released or a minute has elapsed
                await Task.WhenAny(sem.WaitAsync(opts.CancellationToken), Task.Delay(60000));
            }
        }
    }
}