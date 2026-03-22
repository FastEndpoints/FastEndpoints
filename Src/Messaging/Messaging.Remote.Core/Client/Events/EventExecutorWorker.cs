using FastEndpoints.Messaging.Remote;
using FastEndpoints.Messaging.Remote.Core;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

/// <summary>
/// fetches persisted event records from storage and dispatches them to handler instances for execution.
/// extracted from EventSubscriber to isolate the executor loop as a self-contained unit.
/// </summary>
static class EventExecutorWorker
{
    internal static async Task RunAsync<TEvent, TEventHandler, TStorageRecord, TStorageProvider>(TStorageProvider storage,
                                                                                                 SubscriberStorageBehavior storageBehavior,
                                                                                                 SemaphoreSlim sem,
                                                                                                 CallOptions opts,
                                                                                                 int maxConcurrency,
                                                                                                 string subscriberID,
                                                                                                 string eventTypeName,
                                                                                                 ILogger logger,
                                                                                                 ObjectFactory handlerFactory,
                                                                                                 IServiceProvider serviceProvider,
                                                                                                 SubscriberExceptionReceiver? errorReceiver)
        where TEvent : class, IEvent
        where TEventHandler : class, IEventHandler<TEvent>
        where TStorageRecord : class, IEventStorageRecord, new()
        where TStorageProvider : IEventSubscriberStorageProvider<TStorageRecord>
    {
        var ctx = new SubscriberContext(logger, subscriberID, eventTypeName);
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
                        var fetchLimit = storageBehavior.GetFetchLimit(maxConcurrency, executions.Count);

                        var fetchedRecords = await storage.GetNextBatchAsync(
                                                 new()
                                                 {
                                                     CancellationToken = opts.CancellationToken,
                                                     EventType = eventTypeName,
                                                     Limit = fetchLimit,
                                                     SubscriberID = subscriberID,
                                                     Match = e => e.SubscriberID == subscriberID &&
                                                                  e.EventType == eventTypeName &&
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
                        await ctx.InvokeExceptionReceiverSafely(
                            () => errorReceiver?.OnGetNextEventRecordError<TEvent>(subscriberID, retrievalErrorCount, ex, opts.CancellationToken),
                            "get-next-batch");
                        logger.StorageGetNextBatchError(subscriberID, eventTypeName, ex.Message);
                        await Task.Delay(SubscriberTimings.StorageRetrievalErrorDelay, opts.CancellationToken);

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
                                logger.EmptyTrackingIdWarning(subscriberID, eventTypeName);

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
            logger.EventExecutorTaskTerminatedCritical(subscriberID, eventTypeName, ex.Message);
        }
        finally
        {
            await DrainExecutionsAsync();
        }

        async Task WaitForSignalAsync()
        {
            try
            {
                if (await sem.WaitAsync(SubscriberTimings.ExecutorPollInterval, opts.CancellationToken)) //wait for poll interval, semaphore release or app shutdown.
                    while (sem.Wait(0)) { }                                                              // passing app cancellation here is not needed as it's an immediate return.
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
                    logger.EventExecutionCompletionObservedCritical(ex, subscriberID, eventTypeName);
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
                    catch (OperationCanceledException) when (opts.CancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        executionErrorCount++;
                        await ctx.InvokeExceptionReceiverSafely(
                            () => errorReceiver?.OnHandlerExecutionError<TEvent, TEventHandler>(record, executionErrorCount, ex, opts.CancellationToken),
                            "handler-execution");
                        logger.HandlerExecutionCritical(eventTypeName, ex.Message);

                        if (opts.CancellationToken.IsCancellationRequested)
                            return;

                        if (!storageBehavior.ShouldRetryHandlerOnFailure)
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
                        await Task.Delay(SubscriberTimings.HandlerExecutionRetryDelay, opts.CancellationToken);
                    }
                }

                if (storageBehavior.ShouldMarkComplete)
                {
                    record.IsComplete = true;

                    // if opts.CancellationToken is used here, ORMs could throw OperationCanceledException during
                    // shutdown without actually persisting the completion update, causing the event to be replayed.
                    await ctx.RetryUntilSuccess(
                        operation: () => storage.MarkEventAsCompleteAsync(record, CancellationToken.None),
                        onError: (count, ex) => errorReceiver?.OnMarkEventAsCompleteError<TEvent>(record, count, ex, opts.CancellationToken),
                        logError: msg => logger.StorageMarkAsCompleteError(subscriberID, eventTypeName, msg),
                        retryDelay: SubscriberTimings.MarkCompleteRetryDelay,
                        ct: opts.CancellationToken,
                        operationName: "mark-as-complete");
                }
            }
            catch (OperationCanceledException) when (opts.CancellationToken.IsCancellationRequested)
            {
                //graceful shutdown. cancellation is expected.
            }
            catch (Exception ex)
            {
                logger.EventExecutionTaskCritical(ex, subscriberID, eventTypeName);
            }
        }
    }
}