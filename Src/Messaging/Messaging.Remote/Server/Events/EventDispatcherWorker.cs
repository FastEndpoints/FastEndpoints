// ReSharper disable MethodSupportsCancellation

using FastEndpoints.Messaging.Remote.Core;
using Grpc.Core;

namespace FastEndpoints;

/// <summary>
/// dispatches persisted event records to a connected subscriber over a gRPC server stream.
/// extracted from EventHub to isolate the dispatcher loop as a self-contained unit.
/// </summary>
static class EventDispatcherWorker
{
    internal static async Task RunAsync<TEvent, TStorageRecord, TStorageProvider>(TStorageProvider storage,
                                                                                  HubStorageBehavior storageBehavior,
                                                                                  SubscriberRegistry registry,
                                                                                  HubContext ctx,
                                                                                  string subscriberID,
                                                                                  IServerStreamWriter<TEvent> stream,
                                                                                  CancellationToken connectionCt)
        where TEvent : class, IEvent
        where TStorageRecord : class, IEventStorageRecord, new()
        where TStorageProvider : IEventHubStorageProvider<TStorageRecord>
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(connectionCt, ctx.AppCancellation);
        var connectionRegistered = false;

        try
        {
            var retrievalErrorCount = 0;
            var subscriber = registry.RegisterConnection(subscriberID);
            connectionRegistered = true;
            var subscriberSem = subscriber.Sem;

            while (!cts.Token.IsCancellationRequested)
            {
                List<TStorageRecord> records;

                try
                {
                    records = (await storage.GetNextBatchAsync(
                                   new()
                                   {
                                       CancellationToken = cts.Token,
                                       EventType = ctx.EventTypeName,
                                       Limit = EventHubTimings.BatchSize,
                                       SubscriberID = subscriberID,
                                       Match = e => e.SubscriberID == subscriberID &&
                                                    e.EventType == ctx.EventTypeName &&
                                                    !e.IsComplete &&
                                                    DateTime.UtcNow <= e.ExpireOn
                                   })).ToList();
                    retrievalErrorCount = 0;
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    retrievalErrorCount++;
                    await ctx.InvokeExceptionReceiverSafely(() => ctx.Errors?.OnGetNextBatchError<TEvent>(subscriberID, retrievalErrorCount, ex, cts.Token));
                    ctx.Logger.StorageGetNextBatchError(subscriberID, ctx.EventTypeName, ex.Message);

                    if (!cts.Token.IsCancellationRequested)
                        await Task.Delay(EventHubTimings.StorageRetryDelay);

                    continue;
                }

                if (records.Count == 0)
                {
                    await WaitForSignal(subscriberSem, cts);

                    continue;
                }

                for (var i = 0; i < records.Count; i++)
                {
                    var record = records[i];

                    try
                    {
                        await stream.WriteAsync(record.GetEvent<TEvent>(), cts.Token);
                    }
                    catch
                    {
                        if (storageBehavior.ShouldRequeueOnStreamFailure)
                        {
                            // re-queue the current record and all remaining unattempted records in the batch
                            // since they were already dequeued from the in-memory queue by GetNextBatchAsync.
                            try
                            {
                                await storage.StoreEventsAsync(records[i..], cts.Token);
                            }
                            catch
                            {
                                //it's either canceled or queue is full
                                //ignore and discard event if queue is full
                            }
                        }

                        return; //stream is most likely broken/canceled. exit the method here and let the subscriber re-connect and re-enter the method.
                    }

                    await MarkEventComplete<TEvent, TStorageRecord, TStorageProvider>(storage, storageBehavior, ctx, record, subscriberID, cts);
                }
            }
        }
        finally
        {
            if (connectionRegistered)
                registry.ReleaseConnection(subscriberID);

            cts.Dispose();
        }
    }

    static async Task MarkEventComplete<TEvent, TStorageRecord, TStorageProvider>(TStorageProvider storage,
                                                                                  HubStorageBehavior storageBehavior,
                                                                                  HubContext ctx,
                                                                                  TStorageRecord record,
                                                                                  string subscriberID,
                                                                                  CancellationTokenSource cts)
        where TEvent : class, IEvent
        where TStorageRecord : class, IEventStorageRecord, new()
        where TStorageProvider : IEventHubStorageProvider<TStorageRecord>
    {
        if (!storageBehavior.ShouldMarkComplete)
            return;

        record.IsComplete = true;

        // use composite cancellation token (signals client canceled or app shutdown) here so an interrupted "post write ack" leaves the record pending
        // for "at least once" redelivery if the client did not durably persist the event yet.
        await ctx.RetryUntilSuccess(
            operation: () => storage.MarkEventAsCompleteAsync(record, cts.Token),
            onError: (count, ex) => ctx.Errors?.OnMarkEventAsCompleteError<TEvent>(record, count, ex, cts.Token),
            logError: msg => ctx.Logger.StorageMarkAsCompleteError(subscriberID, ctx.EventTypeName, msg),
            retryDelay: EventHubTimings.StorageRetryDelay,
            ct: cts.Token);
    }

    static async Task WaitForSignal(SemaphoreSlim subscriberSem, CancellationTokenSource cts)
    {
        try
        {
            if (await subscriberSem.WaitAsync(EventHubTimings.WaitForSignalTimeout, cts.Token)) //wait for poll interval, semaphore release, or shutdown.
                while (subscriberSem.Wait(0)) { }                                                //drain residual releases so the next poll only runs after new work arrives.
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            //don't throw. let the main loop exit naturally so the disconnect state is updated.
        }
        catch (ObjectDisposedException)
        {
            cts.Cancel();
        }
    }
}
