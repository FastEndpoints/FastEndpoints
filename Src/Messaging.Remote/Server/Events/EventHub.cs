using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using System.Collections.Concurrent;

namespace FastEndpoints;

internal abstract class EventHubBase
{
    //key: stream ID (identifies a unique subscriber from each remote client)
    //val: event queue for the unique client
    protected static readonly ConcurrentDictionary<string, EventQueue> _subscribers = new();

    static EventHubBase()
    {
        _ = RemoveStaleSubscribers();
    }

    private static async Task RemoveStaleSubscribers()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromHours(1));
            foreach (var q in _subscribers)
            {
                if (q.Value.IsStale)
                    _subscribers.TryRemove(q);
            }
        }
    }
}

internal sealed class EventHub<TEvent> : EventHubBase, IMethodBinder<EventHub<TEvent>> where TEvent : class, IEvent
{
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

        ctx.AddServerStreamingMethod(method, metadata, OnClientSubscribed);
    }

    private async Task OnClientSubscribed(EventHub<TEvent> _,
                                          string streamID,
                                          IServerStreamWriter<TEvent> stream,
                                          ServerCallContext ctx)
    {
        var q = _subscribers.GetOrAdd(streamID, new EventQueue());

        while (!ctx.CancellationToken.IsCancellationRequested)
        {
            if (q.IsStale)
                break;

            if (!q.IsEmpty && q.TryPeek(out var evnt))
            {
                try
                {
                    await stream.WriteAsync((TEvent)evnt, ctx.CancellationToken);
                }
                catch (Exception ex)
                {
                    break;
                }

                while (!q.TryDequeue(out var _))
                    await Task.Delay(100);

                q.LastDeQueueAt = DateTime.UtcNow;
            }
            else
            {
                await Task.Delay(500);
            }

        }
    }

    internal static void BroadcastEvent(TEvent evnt)
    {
        foreach (var q in _subscribers.Values)
            q.Enqueue(evnt);
    }
}
