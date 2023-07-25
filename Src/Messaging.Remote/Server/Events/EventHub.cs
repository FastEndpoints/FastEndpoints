using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FastEndpoints;

internal sealed class EventHub<TEvent> : IMethodBinder<EventHub<TEvent>> where TEvent : class, IEvent
{
#pragma warning disable RCS1158
    internal static HubMode Mode = HubMode.EventPublisher;
    internal static ILogger Logger = default!;
    internal static EventHubExceptionReceiver? Errors;
#pragma warning restore RCS1158

    //key: subscriber id
    private static readonly ConcurrentDictionary<string, bool> _subscribers = new();
    private static readonly Type _eventType = typeof(TEvent);

    static EventHub()
    {
        var t = EventHubStorage.Provider.RestoreSubsriberIDsForEventType(typeof(TEvent).FullName!);

        while (!t.IsCompleted)
            Thread.Sleep(1);

        foreach (var subID in t.Result)
            _subscribers[subID] = false;
    }

    public void Bind(ServiceMethodProviderContext<EventHub<TEvent>> ctx)
    {
        var metadata = new List<object>();
        var eventAttributes = _eventType.GetCustomAttributes(false);
        if (eventAttributes?.Length > 0) metadata.AddRange(eventAttributes);
        metadata.Add(new HttpMethodMetadata(new[] { "POST" }, acceptCorsPreflight: true));

        var sub = new Method<string, TEvent>(
            type: MethodType.ServerStreaming,
            serviceName: _eventType.FullName!,
            name: "sub",
            requestMarshaller: new MessagePackMarshaller<string>(),
            responseMarshaller: new MessagePackMarshaller<TEvent>());

        ctx.AddServerStreamingMethod(sub, metadata, OnSubscriberConnected);

        if (Mode is HubMode.EventBroker)
        {
            var pub = new Method<TEvent, EmptyObject>(
                type: MethodType.Unary,
                serviceName: _eventType.FullName!,
                name: "pub",
                requestMarshaller: new MessagePackMarshaller<TEvent>(),
                responseMarshaller: new MessagePackMarshaller<EmptyObject>());

            ctx.AddUnaryMethod(pub, metadata, OnEventReceived);
        }
    }

    internal async Task OnSubscriberConnected(EventHub<TEvent> _, string subscriberID, IServerStreamWriter<TEvent> stream, ServerCallContext ctx)
    {
        Logger.SubscriberConnected(subscriberID, _eventType.FullName!);

        var retrievalErrorCount = 0;
        var updateErrorCount = 0;

        while (!ctx.CancellationToken.IsCancellationRequested)
        {
            _subscribers.GetOrAdd(subscriberID, false);

            IEventStorageRecord? evntRecord;

            try
            {
                evntRecord = await EventHubStorage.Provider.GetNextEventAsync(subscriberID, ctx.CancellationToken);
                retrievalErrorCount = 0;
            }
            catch (Exception ex)
            {
                retrievalErrorCount++;
                Errors?.OnGetNextEventRecordError<TEvent>(subscriberID, retrievalErrorCount, ex, ctx.CancellationToken);
                Logger.StorageRetrieveError(subscriberID, _eventType.FullName!, ex.Message);
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
                        evntRecord.IsComplete = true;
                        await EventHubStorage.Provider.MarkEventAsCompleteAsync(evntRecord, ctx.CancellationToken);
                        updateErrorCount = 0;
                        break;
                    }
                    catch (Exception ex)
                    {
                        updateErrorCount++;
                        Errors?.OnMarkEventAsCompleteError<TEvent>(evntRecord, updateErrorCount, ex, ctx.CancellationToken);
                        Logger.StorageUpdateError(subscriberID, _eventType.FullName!, ex.Message);
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

    internal Task<EmptyObject> OnEventReceived(EventHub<TEvent> __, TEvent evnt, ServerCallContext ctx)
    {
        _ = AddToSubscriberQueues(evnt, ctx.CancellationToken);
        return Task.FromResult(EmptyObject.Instance);
    }

    internal static async Task AddToSubscriberQueues(TEvent evnt, CancellationToken ct)
    {
        var createErrorCount = 0;

        foreach (var subId in _subscribers.Keys)
        {
            var record = EventHubStorage.RecordFactory();
            record.SubscriberID = subId;
            record.Event = evnt;
            record.EventType = _eventType.FullName!;
            record.ExpireOn = DateTime.UtcNow.AddHours(4);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await EventHubStorage.Provider.StoreEventAsync(record, ct);
                    createErrorCount = 0;
                    break;
                }
                catch (OverflowException)
                {
                    _subscribers.Remove(subId, out _);
                    Errors?.OnInMemoryQueueOverflow<TEvent>(record, ct);
                    Logger.QueueOverflowWarning(subId, _eventType.FullName!);
                    break;
                }
                catch (Exception ex)
                {
                    createErrorCount++;
                    Errors?.OnStoreEventRecordError<TEvent>(record, createErrorCount, ex, ct);
                    Logger.StorageCreateError(subId, _eventType.FullName!, ex.Message);
#pragma warning disable CA2016
                    await Task.Delay(5000);
#pragma warning restore CA2016
                }
            }
        }
    }
}