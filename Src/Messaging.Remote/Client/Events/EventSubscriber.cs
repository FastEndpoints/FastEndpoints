using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

internal interface IEventSubscriber
{
    void Start(CallOptions opts);
}

internal sealed class EventSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider> : BaseCommandExecutor<string, TEvent>, ICommandExecutor, IEventSubscriber
    where TEvent : class, IEvent
    where TEventHandler : IEventHandler<TEvent>
    where TStorageRecord : IEventStorageRecord, new()
    where TStorageProvider : IEventSubscriberStorageProvider<TStorageRecord>
{
    private static TStorageProvider? _storage;
    private static bool _isInMemProvider;

    private readonly SemaphoreSlim _sem = new(0);
    private readonly ObjectFactory _handlerFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly SubscriberExceptionReceiver? _errorReceiver;
    private readonly ILogger<EventSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider>>? _logger;
    private readonly string _subscriberID;

    public EventSubscriber(GrpcChannel channel, IServiceProvider serviceProvider)
        : base(channel: channel,
               methodType: MethodType.ServerStreaming,
               endpointName: $"{typeof(TEvent).FullName}/sub")
    {
        _subscriberID = (Environment.MachineName + GetType().FullName + channel.Target).ToHash();
        _serviceProvider = serviceProvider;
        _storage ??= (TStorageProvider)ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, typeof(TStorageProvider));
        _isInMemProvider = _storage is InMemoryEventSubscriberStorage;
        EventSubscriberStorage<TStorageRecord, TStorageProvider>.Provider = _storage; //setup stale record purge task
        EventSubscriberStorage<TStorageRecord, TStorageProvider>.IsInMemProvider = _isInMemProvider;
        _handlerFactory = ActivatorUtilities.CreateFactory(typeof(TEventHandler), Type.EmptyTypes);
        _errorReceiver = _serviceProvider.GetService<SubscriberExceptionReceiver>();
        _logger = serviceProvider.GetRequiredService<ILogger<EventSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider>>>();
        _logger?.SubscriberRegistered(_subscriberID, typeof(TEventHandler).FullName!, typeof(TEvent).FullName!);
    }

    public void Start(CallOptions opts)
    {
        _ = EventReceiverTask(_storage!, _sem, opts, _invoker, _method, _subscriberID, _logger, _errorReceiver);
        _ = EventExecutorTask(_storage!, _sem, opts, _subscriberID, _logger, _handlerFactory, _serviceProvider, _errorReceiver);
    }

    private static async Task EventReceiverTask(TStorageProvider storage, SemaphoreSlim sem, CallOptions opts, CallInvoker invoker, Method<string, TEvent> method, string subscriberID, ILogger? logger, SubscriberExceptionReceiver? errors)
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
                        Event = call.ResponseStream.Current,
                        EventType = typeof(TEvent).FullName!,
                        ExpireOn = DateTime.UtcNow.AddHours(4)
                    };

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
                            logger?.StorageCreateError(subscriberID, typeof(TEvent).FullName!, ex.Message);
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
                logger?.ReceiveTrace(subscriberID, typeof(TEvent).FullName!, ex.Message);
                call.Dispose(); //the stream is most likely broken, so dispose it and initialize a new call
                await Task.Delay(5000);
                call = invoker.AsyncServerStreamingCall(method, null, opts, subscriberID);
            }
        }
    }

    private static async Task EventExecutorTask(TStorageProvider storage, SemaphoreSlim sem, CallOptions opts, string subscriberID, ILogger? logger, ObjectFactory handlerFactory, IServiceProvider? serviceProvider, SubscriberExceptionReceiver? errors)
    {
        var retrievalErrorCount = 0;
        var executionErrorCount = 0;
        var updateErrorCount = 0;

        while (!opts.CancellationToken.IsCancellationRequested)
        {
            IEnumerable<TStorageRecord> records;

            try
            {
                records = await storage.GetNextBatchAsync(new()
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
                logger?.StorageRetrieveError(subscriberID, typeof(TEvent).FullName!, ex.Message);
                await Task.Delay(5000);
                continue;
            }

            if (records.Any())
            {
                foreach (var record in records)
                {
                    var handler = (TEventHandler)handlerFactory(serviceProvider!, null);

                    try
                    {
                        await handler.HandleAsync((TEvent)record.Event, opts.CancellationToken);
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
                            await storage.MarkEventAsCompleteAsync(record, opts.CancellationToken);
                            updateErrorCount = 0;
                            break;
                        }
                        catch (Exception ex)
                        {
                            updateErrorCount++;
                            _ = errors?.OnMarkEventAsCompleteError<TEvent>(record, updateErrorCount, ex, opts.CancellationToken);
                            logger?.StorageUpdateError(subscriberID, typeof(TEvent).FullName!, ex.Message);
                            await Task.Delay(5000);
                        }
                    }
                }
            }
            else
            {
                await sem.WaitAsync(opts.CancellationToken);
            }
        }
    }
}