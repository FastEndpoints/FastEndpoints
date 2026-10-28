using FastEndpoints.Messaging.Remote;
using FastEndpoints.Messaging.Remote.Core;
using Grpc.Core;

namespace FastEndpoints;

/// <summary>
/// dispatches one pending event at a time and marks it complete only after the subscriber acknowledges the hub tracking id.
/// </summary>
static class EventDeliveryAckDispatcher
{
    internal static async Task RunAsync<TEvent, TStorageRecord, TStorageProvider>(TStorageProvider storage,
                                                                                  SubscriberRegistry registry,
                                                                                  HubContext ctx,
                                                                                  string subscriberID,
                                                                                  IAsyncStreamReader<EventDeliveryAck> requestStream,
                                                                                  IServerStreamWriter<EventDelivery<TEvent>> stream,
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
            var subscriber = registry.TryRegisterExclusiveConnection(subscriberID);

            if (subscriber is null)
            {
                ctx.Logger.SubscriberAlreadyConnected(subscriberID, ctx.EventTypeName);

                throw new RpcException(new Status(StatusCode.FailedPrecondition, "subscriber already connected"));
            }

            connectionRegistered = true;
            ctx.Logger.SubscriberConnected(subscriberID, ctx.EventTypeName);

            var ackTimeout = GetAckTimeout(storage);
            var subscriberSem = subscriber.Sem;

            while (!cts.Token.IsCancellationRequested)
            {
                var records = await ctx.GetNextNonEmptyBatch<TEvent, TStorageRecord, TStorageProvider>(
                                  storage, subscriberID, 1, subscriberSem, cts, retrievalRetryDelay);

                if (records is null)
                    break;

                var record = records[0];
                TEvent? evnt;

                try
                {
                    evnt = await ctx.DeserializeEvent<TEvent>(record, cts.Token, deserializationRetryDelay);
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    break;
                }

                if (record.TrackingID == Guid.Empty || evnt is null)
                {
                    // cannot be acknowledged. leaving it pending makes every later event for this subscriber wait behind it.
                    if (record.TrackingID == Guid.Empty)
                        ctx.Logger.EmptyDeliveryTrackingId(subscriberID, ctx.EventTypeName);
                    await ctx.MarkEventComplete<TEvent, TStorageRecord, TStorageProvider>(storage, record, subscriberID, cts.Token);

                    if (cts.Token.IsCancellationRequested)
                        break;

                    continue;
                }

                var delivery = new EventDelivery<TEvent>
                {
                    TrackingID = record.TrackingID,
                    Event = evnt,
                    ReplayUntil = record.ExpireOn
                };

                if (!await RunPhaseAsync(
                        (stream, delivery),
                        static async (state, ct) =>
                        {
                            await state.stream.WriteAsync(state.delivery, ct);
                            return true;
                        },
                        record.ExpireOn,
                        ackTimeout,
                        cts.Token,
                        "event delivery write timed out"))
                    return;

                EventDeliveryAck? ack = null;

                if (!await RunPhaseAsync(
                        requestStream,
                        async (reader, ct) =>
                        {
                            if (!await reader.MoveNext(ct))
                                return false;

                            ack = reader.Current;
                            return true;
                        },
                        record.ExpireOn,
                        ackTimeout,
                        cts.Token,
                        "event delivery acknowledgement timed out",
                        () => ctx.Logger.DeliveryAckTimeout(record.TrackingID, subscriberID, ctx.EventTypeName)))
                    return;

                if (ack is null || ack.TrackingID != record.TrackingID)
                {
                    ctx.Logger.DeliveryAckMismatch(subscriberID, ctx.EventTypeName);
                    return;
                }

                await ctx.MarkEventComplete<TEvent, TStorageRecord, TStorageProvider>(storage, record, subscriberID, cts.Token);

                if (cts.Token.IsCancellationRequested)
                    break;
            }
        }
        finally
        {
            if (connectionRegistered)
                registry.ReleaseConnection(subscriberID);

            cts.Dispose();
        }
    }

    static async Task<bool> RunPhaseAsync<TState>(TState state,
                                                 Func<TState, CancellationToken, Task<bool>> operation,
                                                 DateTime replayUntil,
                                                 TimeSpan ackTimeout,
                                                 CancellationToken ct,
                                                 string timeoutDetail,
                                                 Action? onTimeout = null)
    {
        using var phaseCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        phaseCts.CancelAfter(GetPhaseTimeout(replayUntil, ackTimeout, DateTime.UtcNow));

        try
        {
            return await operation(state, phaseCts.Token);
        }
        catch (Exception) when (phaseCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            onTimeout?.Invoke();
            throw new RpcException(new(StatusCode.DeadlineExceeded, timeoutDetail));
        }
        catch
        {
            return false;
        }
    }

    static TimeSpan GetPhaseTimeout(DateTime replayUntil, TimeSpan ackTimeout, DateTime utcNow)
    {
        var remainingLifetime = replayUntil - utcNow;

        return remainingLifetime <= TimeSpan.Zero ? TimeSpan.Zero :
               remainingLifetime < ackTimeout ? remainingLifetime : ackTimeout;
    }

    internal static TimeSpan GetAckTimeout<TStorageRecord>(IEventHubStorageProvider<TStorageRecord> storage)
        where TStorageRecord : class, IEventStorageRecord
    {
        var timeout = storage is IEventHubDeliveryAck<TStorageRecord> ackStorage
                          ? ackStorage.DeliveryAckTimeout
                          : TimeSpan.FromSeconds(30);

        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(IEventHubDeliveryAck<TStorageRecord>.DeliveryAckTimeout));

        return timeout;
    }
}
