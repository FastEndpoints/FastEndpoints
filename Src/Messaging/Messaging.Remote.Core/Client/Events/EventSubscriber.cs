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
    static readonly string _eventTypeName = typeof(TEvent).FullName!;
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
        : base(channel: channel, methodType: MethodType.ServerStreaming, endpointName: $"{_eventTypeName}/sub")
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
        _logger.SubscriberRegistered(_subscriberID, typeof(TEventHandler).FullName!, _eventTypeName);
    }

    public void Start(CallOptions opts)
    {
        _ = EventReceiverTask(_storage!, _sem, opts, Invoker, Method, _subscriberID, _eventRecordExpiry, _logger, _errorReceiver);
        _ = EventExecutorTask(_storage!, _sem, opts, Environment.ProcessorCount, _subscriberID, _logger, _handlerFactory, _serviceProvider, _errorReceiver);
    }

    internal static async Task EventReceiverTask(TStorageProvider storage,
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
                            TrackingID = Guid.NewGuid(),
                            EventType = _eventTypeName,
                            ExpireOn = DateTime.UtcNow.Add(eventRecordExpiry)
                        };
                        record.SetEvent(call.ResponseStream.Current);

                        while (true)
                        {
                            try
                            {
                                // durable providers must persist the received event even during app shutdown to prevent data loss.
                                await storage.StoreEventAsync(record, _isInMemProvider ? opts.CancellationToken : CancellationToken.None);
                                createErrorCount = 0;

                                break;
                            }
                            catch (Exception ex)
                            {
                                createErrorCount++;
                                await InvokeExceptionReceiverSafely(
                                    () => errors?.OnStoreEventRecordError<TEvent>(record, createErrorCount, ex, opts.CancellationToken),
                                    logger,
                                    subscriberID,
                                    _eventTypeName,
                                    "store-event");
                                logger.StoreEventError(subscriberID, _eventTypeName, ex.Message);

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
                    await InvokeExceptionReceiverSafely(
                        () => errors?.OnEventReceiveError<TEvent>(subscriberID, receiveErrorCount, ex, opts.CancellationToken),
                        logger,
                        subscriberID,
                        _eventTypeName,
                        "stream-receive");
                    logger.StreamReceiveTrace(subscriberID, _eventTypeName, ex.Message);

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
        catch (OperationCanceledException) when (opts.CancellationToken.IsCancellationRequested)
        {
            //graceful shutdown. cancellation is expected.
        }
        catch (Exception ex)
        {
            logger.EventReceiverTaskTerminatedCritical(subscriberID, _eventTypeName, ex.Message);
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

    internal static async Task EventExecutorTask(TStorageProvider storage,
                                                 SemaphoreSlim sem,
                                                 CallOptions opts,
                                                 int maxConcurrency,
                                                 string subscriberID,
                                                 ILogger logger,
                                                 ObjectFactory handlerFactory,
                                                 IServiceProvider serviceProvider,
                                                 SubscriberExceptionReceiver? errorReceiver)
    {
        maxConcurrency = Math.Max(1, maxConcurrency); //always guarantee at least 1 worker slot in case of bad config.

        var retrievalErrorCount = 0;
        var executions = new Dictionary<Guid, Task>();

        try
        {
            while (!opts.CancellationToken.IsCancellationRequested)
            {
                await ObserveCompletedExecutions();

                if (executions.Count < maxConcurrency)
                {
                    List<TStorageRecord> records;

                    try
                    {
                        // for in-memory providers, fetching dequeues records from the queue, so only fetch
                        // exactly the number of available slots to prevent losing events that can't be
                        // immediately assigned to an execution slot. durable providers do not lease records,
                        // so a refill may need to look past "still running" records and requires a full
                        // concurrency-sized window.
                        var fetchLimit = _isInMemProvider
                                             ? maxConcurrency - executions.Count
                                             : maxConcurrency;

                        var fetchedRecords = await storage.GetNextBatchAsync(
                                                 new()
                                                 {
                                                     CancellationToken = opts.CancellationToken,
                                                     EventType = _eventTypeName,
                                                     Limit = fetchLimit,
                                                     SubscriberID = subscriberID,
                                                     Match = e => e.SubscriberID == subscriberID &&
                                                                  e.EventType == _eventTypeName &&
                                                                  !e.IsComplete &&
                                                                  DateTime.UtcNow <= e.ExpireOn
                                                 });
                        records = fetchedRecords.ToList();
                        retrievalErrorCount = 0;
                    }
                    catch (OperationCanceledException) when (opts.CancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        retrievalErrorCount++;
                        await InvokeExceptionReceiverSafely(
                            () => errorReceiver?.OnGetNextEventRecordError<TEvent>(subscriberID, retrievalErrorCount, ex, opts.CancellationToken),
                            logger,
                            subscriberID,
                            _eventTypeName,
                            "get-next-batch");
                        logger.StorageGetNextBatchError(subscriberID, _eventTypeName, ex.Message);
                        await Task.Delay(5000, opts.CancellationToken);

                        continue;
                    }

                    if (records.Count > 0)
                    {
                        var availableSlots = maxConcurrency - executions.Count;

                        foreach (var record in records)
                        {
                            if (availableSlots == 0)
                                break;

                            if (record.TrackingID == Guid.Empty)
                                logger.EmptyTrackingIdWarning(subscriberID, _eventTypeName);

                            if (executions.ContainsKey(record.TrackingID))
                                continue;

                            executions[record.TrackingID] = ExecuteEvent(record);
                            availableSlots--;
                        }

                        if (executions.Count == maxConcurrency)
                            continue;
                    }

                    await WaitForSignalAsync();

                    continue;
                }

                await Task.WhenAny(executions.Values);
            }
        }
        catch (OperationCanceledException) when (opts.CancellationToken.IsCancellationRequested)
        {
            //graceful shutdown. cancellation is expected.
        }
        catch (Exception ex)
        {
            logger.EventExecutorTaskTerminatedCritical(subscriberID, _eventTypeName, ex.Message);
        }
        finally
        {
            await DrainExecutionsAsync();
        }

        async Task WaitForSignalAsync()
        {
            try
            {
                if (await sem.WaitAsync(TimeSpan.FromSeconds(10), opts.CancellationToken)) //wait for poll interval 10secs, semaphore release or app shutdown.
                    while (sem.Wait(0)) { }                                                // passing app cancellation here is not needed as it's an immediate return.
            }
            catch (OperationCanceledException) when (opts.CancellationToken.IsCancellationRequested)
            {
                //don't throw. let the main loop exit via its condition check so DrainExecutionsAsync runs.
            }
        }

        async Task DrainExecutionsAsync()
        {
            await ObserveCompletedExecutions();

            while (executions.Count > 0)
            {
                await Task.WhenAny(executions.Values);
                await ObserveCompletedExecutions();
            }
        }

        async Task ObserveCompletedExecutions()
        {
            if (executions.Count == 0)
                return;

            foreach (var kv in executions.Where(static kv => kv.Value.IsCompleted).ToArray())
            {
                executions.Remove(kv.Key);

                try
                {
                    await kv.Value;
                }
                catch (OperationCanceledException) when (opts.CancellationToken.IsCancellationRequested)
                {
                    //graceful shutdown. cancellation is expected.
                }
                catch (Exception ex)
                {
                    logger.EventExecutionCompletionObservedCritical(ex, subscriberID, _eventTypeName);
                }
            }
        }

        //ensure this method never surfaces any exceptions!
        async Task ExecuteEvent(TStorageRecord record)
        {
            try
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
                        await InvokeExceptionReceiverSafely(
                            () => errorReceiver?.OnHandlerExecutionError<TEvent, TEventHandler>(record, executionErrorCount, ex, opts.CancellationToken),
                            logger,
                            subscriberID,
                            _eventTypeName,
                            "handler-execution");
                        logger.HandlerExecutionCritical(_eventTypeName, ex.Message);

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

                        //prevent instant re-execution
                        await Task.Delay(5000, opts.CancellationToken);
                    }
                }

                var markCompletionErrorCount = 0;

                while (!_isInMemProvider)
                {
                    try
                    {
                        record.IsComplete = true;

                        // if opts.CancellationToken is used here, ORMs could throw OperationCanceledException during
                        // shutdown without actually persisting the completion update, causing the event to be replayed.
                        await storage.MarkEventAsCompleteAsync(record, CancellationToken.None);

                        break;
                    }
                    catch (Exception ex)
                    {
                        markCompletionErrorCount++;
                        await InvokeExceptionReceiverSafely(
                            () => errorReceiver?.OnMarkEventAsCompleteError<TEvent>(record, markCompletionErrorCount, ex, opts.CancellationToken),
                            logger,
                            subscriberID,
                            _eventTypeName,
                            "mark-as-complete");
                        logger.StorageMarkAsCompleteError(subscriberID, _eventTypeName, ex.Message);

                        if (opts.CancellationToken.IsCancellationRequested)
                            break;

                        await Task.Delay(5000, opts.CancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (opts.CancellationToken.IsCancellationRequested)
            {
                //graceful shutdown. cancellation is expected.
            }
            catch (Exception ex)
            {
                logger.EventExecutionTaskCritical(ex, subscriberID, _eventTypeName);
            }
        }
    }

    static async Task InvokeExceptionReceiverSafely(Func<Task?> callbackFactory, ILogger logger, string subscriberID, string eventType, string operation)
    {
        try
        {
            var callback = callbackFactory();

            if (callback is null)
                return;

            // exception receiver hooks are user extension points. await them so any state changes they make are visible
            // to the retry loop, but never let a faulty callback tear down the subscriber worker.
            await callback;
        }
        catch (Exception ex)
        {
            logger.SubscriberExceptionReceiverFault(ex, operation, subscriberID, eventType);
        }
    }
}