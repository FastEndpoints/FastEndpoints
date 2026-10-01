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
                                                                                  CancellationToken connectionCt,
                                                                                  TimeSpan? deserializationRetryDelay = null,
                                                                                  TimeSpan? retrievalRetryDelay = null)
        where TEvent : class, IEvent
        where TStorageRecord : class, IEventStorageRecord, new()
        where TStorageProvider : IEventHubStorageProvider<TStorageRecord>
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(connectionCt, ctx.AppCancellation);
        var connectionRegistered = false;

        try
        {
            var subscriber = registry.RegisterConnection(subscriberID);
            connectionRegistered = true;
            var subscriberSem = subscriber.Sem;

            while (!cts.Token.IsCancellationRequested)
            {
                var records = await ctx.GetNextNonEmptyBatch<TEvent, TStorageRecord, TStorageProvider>(
                                  storage, subscriberID, EventHubSettings.BatchSize, subscriberSem, cts, retrievalRetryDelay);

                if (records is null)
                    break;

                for (var i = 0; i < records.Count; i++)
                {
                    var record = records[i];

                    TEvent? evnt;

                    try
                    {
                        evnt = await ctx.DeserializeEvent<TEvent>(record, cts.Token, deserializationRetryDelay);
                    }
                    catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                    {
                        await RequeueBatchAsync(storage, storageBehavior, records, i, CancellationToken.None);

                        return;
                    }

                    if (evnt is null)
                    {
                        await ctx.MarkEventComplete<TEvent, TStorageRecord, TStorageProvider>(storage, storageBehavior, record, subscriberID, cts.Token);

                        continue;
                    }

                    try
                    {
                        await stream.WriteAsync(evnt, cts.Token);
                    }
                    catch
                    {
                        await RequeueBatchAsync(storage, storageBehavior, records, i, cts.Token);

                        return; //stream is most likely broken/canceled. exit the method here and let the subscriber re-connect and re-enter the method.
                    }

                    await ctx.MarkEventComplete<TEvent, TStorageRecord, TStorageProvider>(storage, storageBehavior, record, subscriberID, cts.Token);
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

    static async ValueTask RequeueBatchAsync<TStorageRecord>(IEventHubStorageProvider<TStorageRecord> storage,
                                                           HubStorageBehavior storageBehavior,
                                                           List<TStorageRecord> records,
                                                           int index,
                                                           CancellationToken ct)
        where TStorageRecord : class, IEventStorageRecord
    {
        if (!storageBehavior.ShouldRequeueOnStreamFailure)
            return;

        try
        {
            // in-memory reads dequeue the batch, so recover the current record and unattempted suffix.
            await storage.StoreEventsAsync(records[index..], ct);
        }
        catch
        {
            // recovery is best-effort when canceled or the queue is full.
        }
    }
}
