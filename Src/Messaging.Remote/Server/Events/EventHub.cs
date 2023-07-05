using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FastEndpoints;

internal sealed class EventHub<TEvent> : IMethodBinder<EventHub<TEvent>> where TEvent : class, IEvent
{
#pragma warning disable RCS1158
    internal static ILogger Logger = default!;
    internal static PublisherExceptionReceiver? Errors;
#pragma warning restore RCS1158

    //key: subscriber id
    private static readonly ConcurrentDictionary<string, bool> _subscribers = new();
    private static readonly Type _eventType = typeof(TEvent);

    static EventHub()
    {
        var t = EventPublisherStorage.Provider.RestoreSubsriberIDsForEventType(typeof(TEvent).FullName!);

        while (!t.IsCompleted)
            Thread.Sleep(1);

        foreach (var subID in t.Result)
            _subscribers[subID] = false;
    }

    public void Bind(ServiceMethodProviderContext<EventHub<TEvent>> ctx)
    {
        var method = new Method<string, TEvent>(
            type: MethodType.ServerStreaming,
            serviceName: _eventType.FullName!,
            name: "",
            requestMarshaller: new MessagePackMarshaller<string>(),
            responseMarshaller: new MessagePackMarshaller<TEvent>());

        var metadata = new List<object>();
        var handlerAttributes = _eventType.GetCustomAttributes(false);
        if (handlerAttributes?.Length > 0) metadata.AddRange(handlerAttributes);
        metadata.Add(new HttpMethodMetadata(new[] { "POST" }, acceptCorsPreflight: true));

        ctx.AddServerStreamingMethod(method, metadata, OnClientConnected);
    }

    internal async Task OnClientConnected(EventHub<TEvent> _, string subscriberID, IServerStreamWriter<TEvent> stream, ServerCallContext ctx)
    {
        Logger.LogInformation("Event subscriber connected! [id:{subid}]({tevent})", subscriberID, _eventType.FullName!);

        var retrievalErrorCount = 0;
        var updateErrorCount = 0;

        while (!ctx.CancellationToken.IsCancellationRequested)
        {
            _subscribers.GetOrAdd(subscriberID, false);

            IEventStorageRecord? evntRecord;

            try
            {
                evntRecord = await EventPublisherStorage.Provider.GetNextEventAsync(subscriberID, ctx.CancellationToken);
                retrievalErrorCount = 0;
            }
            catch (Exception ex)
            {
                retrievalErrorCount++;
                Errors?.OnGetNextEventRecordError<TEvent>(subscriberID, retrievalErrorCount, ex, ctx.CancellationToken);
                Logger.LogError("Event storage 'retrieval' error for [subscriber-id:{subid}]({tevent}): {msg}. Retrying in 5 seconds...",
                    subscriberID,
                    _eventType.FullName,
                    ex.Message);
                await Task.Delay(5000);
                continue;
            }

            if (evntRecord is not null)
            {
                try
                {
                    await stream.WriteAsync((TEvent)evntRecord.Event, ctx.CancellationToken);
                }
                catch
                {
                    break; //stream is most likely broken/cancelled. let the client re-connect.
                }

                while (!ctx.CancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await EventPublisherStorage.Provider.MarkEventAsCompleteAsync(evntRecord, ctx.CancellationToken);
                        updateErrorCount = 0;
                        break;
                    }
                    catch (Exception ex)
                    {
                        updateErrorCount++;
                        Errors?.OnMarkEventAsCompleteError<TEvent>(evntRecord, updateErrorCount, ex, ctx.CancellationToken);
                        Logger.LogError("Event storage 'update' error for [subscriber-id:{subid}]({tevent}): {msg}. Retrying in 5 seconds...",
                            subscriberID,
                            _eventType.FullName,
                            ex.Message);
                        await Task.Delay(5000);
                    }
                }
            }
            else
            {
                await Task.Delay(300);
            }
        }
    }

    internal static async Task AddToSubscriberQueues(TEvent evnt, CancellationToken ct)
    {
        var createErrorCount = 0;

        foreach (var subId in _subscribers.Keys)
        {
            var record = EventPublisherStorage.RecordFactory();
            record.SubscriberID = subId;
            record.Event = evnt;
            record.EventType = _eventType.FullName!;
            record.ExpireOn = DateTime.UtcNow.AddHours(4);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await EventPublisherStorage.Provider.StoreEventAsync(record, ct);
                    createErrorCount = 0;
                    break;
                }
                catch (OverflowException)
                {
                    _subscribers.Remove(subId, out _);
                    Errors?.OnInMemoryQueueOverflow<TEvent>(record, ct);
                    Logger.LogWarning("Event queue for [subscriber-id:{subid}]({tevent}) is full! The subscriber has been removed from the broadcast list.",
                        subId,
                        _eventType.FullName);
                    break;
                }
                catch (Exception ex)
                {
                    createErrorCount++;
                    Errors?.OnStoreEventRecordError<TEvent>(record, createErrorCount, ex, ct);
                    Logger.LogError("Event storage 'create' error for [subscriber-id:{subid}]({tevent}): {msg}. Retrying in 5 seconds...",
                        subId,
                        _eventType.FullName,
                        ex.Message);
                    await Task.Delay(5000);
                }
            }
        }
    }
}
