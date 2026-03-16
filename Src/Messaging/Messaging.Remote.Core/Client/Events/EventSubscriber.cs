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

sealed class EventSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider> : BaseCommandExecutor<string, TEvent>, ICommandExecutor, IEventSubscriber
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
    readonly ILogger<EventSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider>> _logger;
    readonly string _subscriberID;
    readonly TimeSpan _eventRecordExpiry;

    public EventSubscriber(ChannelBase channel, string clientIdentifier, IServiceProvider serviceProvider)
        : this(channel, clientIdentifier, null, serviceProvider) { }

    public EventSubscriber(ChannelBase channel, string clientIdentifier, string? subscriberID, IServiceProvider serviceProvider)
        : base(channel: channel, methodType: MethodType.ServerStreaming, endpointName: $"{typeof(TEvent).FullName}/sub")
    {
        _subscriberID = SubscriberIDFactory.Create(subscriberID, clientIdentifier, GetType(), channel.Target);
        _serviceProvider = serviceProvider;
        _storage ??= (TStorageProvider)ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, typeof(TStorageProvider));
        _isInMemProvider = _storage is InMemoryEventSubscriberStorage;
        EventSubscriberStorage<TStorageRecord, TStorageProvider>.Provider = _storage; //setup stale record purge task
        EventSubscriberStorage<TStorageRecord, TStorageProvider>.IsInMemProvider = _isInMemProvider;
        _handlerFactory = ActivatorUtilities.CreateFactory(typeof(TEventHandler), Type.EmptyTypes);
        _errorReceiver = _serviceProvider.GetService<SubscriberExceptionReceiver>();
        _eventRecordExpiry = RemoteConnectionCore.EventRecordExpiry;
        _logger = serviceProvider.GetRequiredService<ILogger<EventSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider>>>();
        _logger.SubscriberRegistered(_subscriberID, typeof(TEventHandler).FullName!, typeof(TEvent).FullName!);
    }

    public void Start(CallOptions opts)
    {
        _ = EventReceiverTask(_storage!, _sem, opts, Invoker, Method, _subscriberID, _eventRecordExpiry, _logger, _errorReceiver);
        _ = EventExecutorTask(_storage!, _sem, opts, Environment.ProcessorCount, _subscriberID, _logger, _handlerFactory, _serviceProvider, _errorReceiver);
    }

    static async Task EventReceiverTask(TStorageProvider storage,
                                        SemaphoreSlim sem,
                                        CallOptions opts,
                                        CallInvoker invoker,
                                        Method<string, TEvent> method,
                                        string subscriberID,
                                        TimeSpan eventRecordExpiry,
                                        ILogger logger,
                                        SubscriberExceptionReceiver? errors)
    {
        var call = invoker.AsyncServerStreamingCall(method, null, opts, subscriberID);
        var createErrorCount = 0;
        var receiveErrorCount = 0;

        try
        {
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
                            ExpireOn = DateTime.UtcNow.Add(eventRecordExpiry)
                        };
                        record.SetEvent(call.ResponseStream.Current);

                        while (true)
                        {
                            try
                            {
                                await storage.StoreEventAsync(record, opts.CancellationToken);
                                createErrorCount = 0;

                                break;
                            }
                            catch (Exception ex)
                            {
                                createErrorCount++;
                                errors?.OnStoreEventRecordError<TEvent>(record, createErrorCount, ex, opts.CancellationToken);
                                logger.StoreEventError(subscriberID, typeof(TEvent).FullName!, ex.Message);

                                if (opts.CancellationToken.IsCancellationRequested)
                                    break;

                                await Task.Delay(5000, opts.CancellationToken);
                            }
                        }

                        sem.Release();
                        receiveErrorCount = 0;
                    }
                }
                catch (OperationCanceledException) when (opts.CancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    receiveErrorCount++;
                    errors?.OnEventReceiveError<TEvent>(subscriberID, receiveErrorCount, ex, opts.CancellationToken);
                    logger.StreamReceiveTrace(subscriberID, typeof(TEvent).FullName!, ex.Message);

                    try
                    {
                        call.Dispose();
                    }
                    catch
                    {
                        //safe to ignore.
                    }

                    await Task.Delay(5000, opts.CancellationToken);
                    call = invoker.AsyncServerStreamingCall(method, null, opts, subscriberID);
                }
            }
        }
        catch (OperationCanceledException)
        {
            //graceful shutdown. cancellation is expected.
        }
        catch (Exception ex)
        {
            logger.EventReceiverTaskTerminatedCritical(subscriberID, typeof(TEvent).FullName!, ex.Message);
        }
        finally
        {
            try
            {
                call.Dispose();
            }
            catch
            {
                //safe to ignore.
            }
        }
    }

    static async Task EventExecutorTask(TStorageProvider storage,
                                        SemaphoreSlim sem,
                                        CallOptions opts,
                                        int maxConcurrency,
                                        string subscriberID,
                                        ILogger logger,
                                        ObjectFactory handlerFactory,
                                        IServiceProvider serviceProvider,
                                        SubscriberExceptionReceiver? errorReceiver)
    {
        var retrievalErrorCount = 0;

        try
        {
            while (!opts.CancellationToken.IsCancellationRequested)
            {
                IEnumerable<TStorageRecord> records;

                try
                {
                    records = await storage.GetNextBatchAsync(
                                  new()
                                  {
                                      CancellationToken = opts.CancellationToken,
                                      Limit = maxConcurrency,
                                      SubscriberID = subscriberID,
                                      Match = e => e.SubscriberID == subscriberID && !e.IsComplete && DateTime.UtcNow <= e.ExpireOn
                                  });
                    retrievalErrorCount = 0;
                }
                catch (Exception ex)
                {
                    retrievalErrorCount++;
                    errorReceiver?.OnGetNextEventRecordError<TEvent>(subscriberID, retrievalErrorCount, ex, opts.CancellationToken);
                    logger.StorageGetNextBatchError(subscriberID, typeof(TEvent).FullName!, ex.Message);
                    await Task.Delay(5000, opts.CancellationToken);

                    continue;
                }

                if (records.Any())
                {
                    await Task.WhenAll(records.Select(ExecuteEvent)); //Parallel.ForEachAsync not available in netstandard2.1

                    //ensure this method never surfaces any exceptions!
                    async Task ExecuteEvent(TStorageRecord record)
                    {
                        var executionErrorCount = 0;

                        while (!opts.CancellationToken.IsCancellationRequested)
                        {
                            var handler = handlerFactory.GetEventHandlerOrCreateInstance<TEvent, TEventHandler>(serviceProvider);

                            try
                            {
                                await handler.HandleAsync(record.GetEvent<TEvent>(), opts.CancellationToken);

                                break; //handler succeeded, exit retry loop
                            }
                            catch (Exception ex)
                            {
                                executionErrorCount++;
                                errorReceiver?.OnHandlerExecutionError<TEvent, TEventHandler>(record, executionErrorCount, ex, opts.CancellationToken);
                                logger.HandlerExecutionCritical(typeof(TEvent).FullName!, ex.Message);

                                if (opts.CancellationToken.IsCancellationRequested)
                                    return;

                                if (_isInMemProvider)
                                {
                                    //for in-memory provider, re-queue the event so it goes to the back of the queue and doesn't permanently block
                                    //this execution slot. limitation: executionErrorCount will always be 1 since the count resets on each dequeue.
                                    try
                                    {
                                        await storage.StoreEventAsync(record, opts.CancellationToken);
                                    }
                                    catch
                                    {
                                        //ignore and discard event when queue is full
                                    }

                                    return;
                                }

                                //prevent instant re-execution and allow `errorReceiver.OnHandlerExecutionError`
                                //some time to modify the record if needed.
                                await Task.Delay(5000, opts.CancellationToken);
                            }
                        }

                        var markCompletionErrorCount = 0;

                        while (!_isInMemProvider)
                        {
                            try
                            {
                                record.IsComplete = true;
                                await storage.MarkEventAsCompleteAsync(record, opts.CancellationToken);
                                markCompletionErrorCount = 0;

                                break;
                            }
                            catch (Exception ex)
                            {
                                markCompletionErrorCount++;
                                errorReceiver?.OnMarkEventAsCompleteError<TEvent>(record, markCompletionErrorCount, ex, opts.CancellationToken);
                                logger.StorageMarkAsCompleteError(subscriberID, typeof(TEvent).FullName!, ex.Message);

                                if (opts.CancellationToken.IsCancellationRequested)
                                    break;

                                await Task.Delay(5000, opts.CancellationToken);
                            }
                        }
                    }
                }
                else
                {
                    //wait until either the semaphore is released or the poll interval has elapsed
                    await Task.WhenAny(sem.WaitAsync(opts.CancellationToken), Task.Delay(10000, opts.CancellationToken));
                }
            }
        }
        catch (OperationCanceledException)
        {
            //graceful shutdown. cancellation is expected.
        }
        catch (Exception ex)
        {
            logger.EventExecutorTaskTerminatedCritical(subscriberID, typeof(TEvent).FullName!, ex.Message);
        }
    }
}
