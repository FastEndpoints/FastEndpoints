using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FastEndpoints;

internal sealed class EventHub<TEvent> : IMethodBinder<EventHub<TEvent>> where TEvent : class, IEvent
{
    internal static ILogger Logger = default!;

    //key: subscriber id
    //val: last dequeue time
    private static readonly ConcurrentDictionary<string, DateTime> _subscribers = new();

    static EventHub()
    {
        var t = EventPublisherStorage.Provider.RestoreSubsriberIDsForEventType(typeof(TEvent).FullName!);

        while (!t.IsCompleted)
            Thread.Sleep(1);

        foreach (var subID in t.Result)
            _subscribers[subID] = DateTime.UtcNow;
    }

    public void Bind(ServiceMethodProviderContext<EventHub<TEvent>> ctx)
    {
        var tEvent = typeof(TEvent);

        var method = new Method<string, TEvent>(
            type: MethodType.ServerStreaming,
            serviceName: tEvent.FullName!,
            name: "",
            requestMarshaller: new MessagePackMarshaller<string>(),
            responseMarshaller: new MessagePackMarshaller<TEvent>());

        var metadata = new List<object>();
        var handlerAttributes = tEvent.GetCustomAttributes(false);
        if (handlerAttributes?.Length > 0) metadata.AddRange(handlerAttributes);
        metadata.Add(new HttpMethodMetadata(new[] { "POST" }, acceptCorsPreflight: true));

        ctx.AddServerStreamingMethod(method, metadata, OnClientConnected);
    }

    private async Task OnClientConnected(EventHub<TEvent> _,
                                         string subscriberID,
                                         IServerStreamWriter<TEvent> stream,
                                         ServerCallContext ctx)
    {
        while (!ctx.CancellationToken.IsCancellationRequested)
        {
            IEventStorageRecord? evntRecord;

            try
            {
                evntRecord = await EventPublisherStorage.Provider.GetNextEventAsync(subscriberID, ctx.CancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError("Event storage 'retrieval' error for [subscriber-id:{subid}]({tevent}): {msg}. Retrying in 5 seconds...",
                    subscriberID,
                    typeof(TEvent).FullName,
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
                        _subscribers[subscriberID] = DateTime.UtcNow;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Event storage 'update' error for [subscriber-id:{subid}]({tevent}): {msg}. Retrying in 5 seconds...",
                            subscriberID,
                            typeof(TEvent).FullName,
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

    internal static async void AddToSubscriberQueues(TEvent evnt, CancellationToken ct)
    {
        foreach (var subId in _subscribers.Keys)
        {
            var record = EventPublisherStorage.RecordFactory();
            record.SubscriberID = subId;
            record.Event = evnt;
            record.EventType = typeof(TEvent).FullName!;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await EventPublisherStorage.Provider.StoreEventAsync(record, ct);
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Event storage 'create' error for [subscriber-id:{subid}]({tevent}): {msg}. Retrying in 5 seconds...",
                        subId,
                        typeof(TEvent).FullName,
                        ex.Message);
                    await Task.Delay(5000);
                }
            }
        }
    }
}
