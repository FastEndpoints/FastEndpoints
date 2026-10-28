using FastEndpoints.Messaging.Remote.Core;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

/// <summary>
/// receives events from a gRPC server stream and persists them via the configured storage provider.
/// extracted from EventSubscriber to isolate the receiver loop as a self-contained unit.
/// </summary>
static class EventReceiverWorker
{
    internal static async Task RunAsync<TEvent, TStorageRecord, TStorageProvider>(TStorageProvider storage,
                                                                                  SubscriberStorageBehavior storageBehavior,
                                                                                  SemaphoreSlim sem,
                                                                                  CallOptions opts,
                                                                                  CallInvoker invoker,
                                                                                  Method<string, TEvent> method,
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
        var ctx = new SubscriberContext(logger, subscriberID, eventTypeName);
        var retryInterval = retryDelay ?? SubscriberTimings.ReceiverReconnectDelay;
        var call = invoker.AsyncServerStreamingCall(method, null, opts, subscriberID);
        var supervisor = new EventReceiveSupervisor<TEvent>(ctx, errors, retryInterval, opts.CancellationToken);

        try
        {
            await supervisor.RunAsync(ReceiveSession, () => call = invoker.AsyncServerStreamingCall(method, null, opts, subscriberID));
        }
        finally
        {
            DisposeCall();
        }

        async Task<ReceiveSessionResult> ReceiveSession()
        {
            try
            {
                while (await call!.ResponseStream.MoveNext(opts.CancellationToken)) // actual network call happens on MoveNext()
                {
                    var record = new TStorageRecord
                    {
                        SubscriberID = subscriberID,
                        TrackingID = Guid.NewGuid(),
                        EventType = eventTypeName,
                        ExpireOn = DateTime.UtcNow.Add(eventRecordExpiry)
                    };
                    record.SetEvent(call.ResponseStream.Current);

                    // durable providers must persist the received event even during app shutdown to prevent data loss.
                    await ctx.RetryStoreEvent<TEvent>(
                        record,
                        operation: () => storage.StoreEventAsync(record, storageBehavior.GetStoreEventToken(opts.CancellationToken)),
                        errors: errors,
                        retryDelay: retryInterval,
                        ct: opts.CancellationToken);

                    sem.Release();
                    supervisor.Received();
                }

                return opts.CancellationToken.IsCancellationRequested ? ReceiveSessionResult.Stop : ReceiveSessionResult.Reconnect;
            }
            finally
            {
                DisposeCall();
            }
        }

        void DisposeCall()
        {
            try
            {
                call?.Dispose();
            }
            catch
            {
                //safe to ignore.
            }

            call = null;
        }
    }
}