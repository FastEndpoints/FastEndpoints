using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using System.Collections.Concurrent;

namespace FastEndpoints;

internal sealed class EventHub<TEvent> : IMethodBinder<EventHub<TEvent>> where TEvent : class, IEvent
{
    //key: subscriber ID (identifies a unique subscriber/client)
    //val: event queue for the unique client
    private static readonly ConcurrentDictionary<string, EventQueue<TEvent>> _subscribers = new();

    static EventHub()
    {
        _ = StaleSubscriberPurgingTask();
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
        var q = _subscribers.GetOrAdd(subscriberID, new EventQueue<TEvent>());

        while (!ctx.CancellationToken.IsCancellationRequested && !q.IsStale)
        {
            if (!q.IsEmpty && q.TryPeek(out var evnt))
            {
                try
                {
                    await stream.WriteAsync(evnt, ctx.CancellationToken);
                }
                catch
                {
                    break; //stream is most likely broken/cancelled
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

    internal static void AddToSubscriberQueues(TEvent evnt)
    {
        foreach (var q in _subscribers.Values)
            q.Enqueue(evnt);
    }

    private static async Task StaleSubscriberPurgingTask()
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
