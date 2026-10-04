using FastEndpoints.Messaging.Remote.Core;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

/// <summary>
/// receives acknowledged deliveries: hello, store the hub tracking id, then ack. a duplicate store is still acknowledged.
/// </summary>
static class EventDeliveryAckReceiver
{
    static async Task<bool> StoreDelivery<TEvent, TStorageRecord, TStorageProvider>(TStorageProvider storage,
                                                                                 SubscriberStorageBehavior storageBehavior,
                                                                                 EventDelivery<TEvent> delivery,
                                                                                 SubscriberContext ctx,
                                                                                 TimeSpan eventRecordExpiry,
                                                                                 TimeSpan allowance,
                                                                                 SubscriberExceptionReceiver? errors,
                                                                                 TimeSpan retryInterval,
                                                                                 CancellationToken ct)
        where TEvent : class, IEvent
        where TStorageRecord : class, IEventStorageRecord, new()
        where TStorageProvider : IEventSubscriberStorageProvider<TStorageRecord>
    {
        var record = new TStorageRecord
        {
            SubscriberID = ctx.SubscriberID,
            TrackingID = delivery.TrackingID,
            EventType = ctx.EventTypeName
        };
        ((IEventDeliveryAckStorageRecord)record).RetainUntil = EventSubscriberRetentionPolicy<TStorageRecord>.GetRetainUntil(delivery.ReplayUntil, allowance);
        record.SetEvent(delivery.Event);

        var accepted = false;

        // duplicate is caught inside the operation so RetryUntilSuccess does not log it and retry forever.
        await ctx.RetryStoreEvent<TEvent>(
            record,
            operation: async () =>
                       {
                           try
                           {
                               record.ExpireOn = DateTime.UtcNow.Add(eventRecordExpiry);
                               await storage.StoreEventAsync(record, storageBehavior.GetStoreEventToken(ct));
                               accepted = true;
                           }
                           catch (DuplicateEventDeliveryException ex)
                           {
                               if (ex.TrackingID == Guid.Empty)
                                   ctx.Logger.DuplicateDeliveryEmptyTrackingIdWarning(ctx.SubscriberID, ctx.EventTypeName);

                               accepted = true;
                           }
                       },
            errors: errors,
            retryDelay: retryInterval,
            ct: ct);

        return accepted;
    }

    internal static async Task RunAsync<TEvent, TStorageRecord, TStorageProvider>(TStorageProvider storage,
                                                                                  SubscriberStorageBehavior storageBehavior,
                                                                                  SemaphoreSlim sem,
                                                                                  CallOptions opts,
                                                                                  CallInvoker invoker,
                                                                                  Method<EventDeliveryAck, EventDelivery<TEvent>> method,
                                                                                  string subscriberID,
                                                                                  string eventTypeName,
                                                                                  TimeSpan eventRecordExpiry,
                                                                                  ILogger logger,
                                                                                  SubscriberExceptionReceiver? errors,
                                                                                  TimeSpan? retryDelay = null)
        where TEvent : class, IEvent
        where TStorageRecord : class, IEventStorageRecord, new()
        where TStorageProvider : IEventSubscriberStorageProvider<TStorageRecord>
    {
        var allowance = EventSubscriberRetentionPolicy<TStorageRecord>.GetClockSkewAllowance(storage);
        var ctx = new SubscriberContext(logger, subscriberID, eventTypeName);
        var retryInterval = retryDelay ?? SubscriberTimings.ReceiverReconnectDelay;
        var supervisor = new EventReceiveSupervisor<TEvent>(ctx, errors, retryInterval, opts.CancellationToken);
        await supervisor.RunAsync(ReceiveSession);

        async Task<ReceiveSessionResult> ReceiveSession()
        {
            AsyncDuplexStreamingCall<EventDeliveryAck, EventDelivery<TEvent>>? call = null;

            try
            {
                call = invoker.AsyncDuplexStreamingCall(method, null, opts);
                await call.RequestStream.WriteAsync(new() { SubscriberID = subscriberID });

                while (await call.ResponseStream.MoveNext(opts.CancellationToken))
                {
                    var delivery = call.ResponseStream.Current;

                    if (delivery.TrackingID == Guid.Empty || delivery.Event is null)
                    {
                        logger.EmptyDeliveryTrackingIdWarning(subscriberID, eventTypeName);

                        break;
                    }

                    if (delivery.ReplayUntil == default)
                    {
                        var error = new InvalidOperationException("The ACK hub must supply ReplayUntil. Upgrade the hub before the subscriber.");
                        await supervisor.ReportErrorAsync(error);
                        logger.EventReceiverTaskTerminatedCritical(subscriberID, eventTypeName, error.Message);

                        return ReceiveSessionResult.Stop;
                    }

                    if (!await StoreDelivery<TEvent, TStorageRecord, TStorageProvider>(
                            storage, storageBehavior, delivery, ctx, eventRecordExpiry, allowance, errors, retryInterval, opts.CancellationToken))
                        break;

                    // a duplicate can follow a committed insert whose response failed before signaling.
                    sem.Release();

                    await call.RequestStream.WriteAsync(new() { TrackingID = delivery.TrackingID });

                    supervisor.Received();
                }

                return opts.CancellationToken.IsCancellationRequested ? ReceiveSessionResult.Stop : ReceiveSessionResult.Reconnect;
            }
            finally
            {
                try
                {
                    call?.Dispose();
                }
                catch
                {
                    //safe to ignore.
                }
            }
        }
    }
}