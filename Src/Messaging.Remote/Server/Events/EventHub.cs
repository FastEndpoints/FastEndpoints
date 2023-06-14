using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using System.Collections.Concurrent;

namespace FastEndpoints;

internal static class EventHubExtensions
{
    public static Task RemotePublishAsync<TEvent>(this TEvent evnt, int? expectedSubscriberCount = null)
        where TEvent : class, IEvent
    {

    }
}

internal static class SubscriberStore<TEvent> where TEvent : IEvent
{
    internal static int ExpectedSubCount { get; set; }

    private static readonly ConcurrentBag<IServerStreamWriter<TEvent>> _subscribers = new();

    internal static void AddSubscriber(IServerStreamWriter<TEvent> stream)
        => _subscribers.Add(stream);
}

internal sealed class EventHub<TEvent> : IMethodBinder<EventHub<TEvent>> where TEvent : class, IEvent
{
    public void Bind(ServiceMethodProviderContext<EventHub<TEvent>> ctx)
    {
        var tEvent = typeof(TEvent);

        var method = new Method<EmptyObject, TEvent>(
            type: MethodType.ServerStreaming,
            serviceName: tEvent.FullName!,
            name: "",
            requestMarshaller: new MessagePackMarshaller<EmptyObject>(),
            responseMarshaller: new MessagePackMarshaller<TEvent>());

        var metadata = new List<object>();
        var handlerAttributes = tEvent.GetCustomAttributes(false);
        if (handlerAttributes?.Length > 0) metadata.AddRange(handlerAttributes);
        metadata.Add(new HttpMethodMetadata(new[] { "POST" }, acceptCorsPreflight: true));

        ctx.AddServerStreamingMethod(method, metadata, OnClientSubscribed);
    }

    private Task OnClientSubscribed(EventHub<TEvent> _,
                                    EmptyObject __,
                                    IServerStreamWriter<TEvent> stream,
                                    ServerCallContext ___)
    {
        SubscriberStore<TEvent>.AddSubscriber(stream);
        return Task.CompletedTask;
    }
}