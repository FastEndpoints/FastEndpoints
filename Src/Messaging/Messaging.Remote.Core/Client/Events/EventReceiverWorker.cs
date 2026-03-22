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
        var receiveErrorCount = 0;

        try
        {
            while (!opts.CancellationToken.IsCancellationRequested)
            {
                bool reconnect;

                try
                {
                    while (await call.ResponseStream.MoveNext(opts.CancellationToken)) // actual network call happens on MoveNext()
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
                        await ctx.RetryUntilSuccess(
                            operation: () => storage.StoreEventAsync(record, storageBehavior.GetStoreEventToken(opts.CancellationToken)),
                            onError: (count, ex) => errors?.OnStoreEventRecordError<TEvent>(record, count, ex, opts.CancellationToken),
                            logError: msg => logger.StoreEventError(subscriberID, eventTypeName, msg),
                            retryDelay: retryInterval,
                            ct: opts.CancellationToken,
                            operationName: "store-event");

                        sem.Release();
                        receiveErrorCount = 0;
                    }

                    reconnect = !opts.CancellationToken.IsCancellationRequested;
                }
                catch (OperationCanceledException) when (opts.CancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    receiveErrorCount++;
                    await ctx.InvokeExceptionReceiverSafely(
                        () => errors?.OnEventReceiveError<TEvent>(subscriberID, receiveErrorCount, ex, opts.CancellationToken),
                        "stream-receive");
                    logger.StreamReceiveTrace(subscriberID, eventTypeName, ex.Message);
                    reconnect = true;
                }

                if (!reconnect)
                    continue;

                try
                {
                    call.Dispose();
                }
                catch
                {
                    //safe to ignore.
                }

                await Task.Delay(retryInterval, opts.CancellationToken);
                call = invoker.AsyncServerStreamingCall(method, null, opts, subscriberID);
            }
        }
        catch (OperationCanceledException) when (opts.CancellationToken.IsCancellationRequested)
        {
            //graceful shutdown. cancellation is expected.
        }
        catch (Exception ex)
        {
            logger.EventReceiverTaskTerminatedCritical(subscriberID, eventTypeName, ex.Message);
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
}