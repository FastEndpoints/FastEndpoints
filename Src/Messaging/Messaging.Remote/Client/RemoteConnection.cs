using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// represents a connection to a remote server that hosts command and event handlers
/// </summary>
public sealed class RemoteConnection : RemoteConnectionCore
{
    internal RemoteConnection(string address, IServiceProvider serviceProvider) : base(address, serviceProvider)
    {
        var httpMsgHnd = serviceProvider.GetService<HttpMessageHandler>();

        if (httpMsgHnd is not null)
            ChannelOptions.HttpHandler = httpMsgHnd;
        else
        {
            ChannelOptions.HttpHandler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(5),
                EnableMultipleHttp2Connections = true
            };
        }
    }

    /// <summary>
    /// register an "event" that the remote server will be accepting (in <see cref="HubMode.EventBroker" />) for distribution to subscribers.
    /// </summary>
    /// <typeparam name="TEvent">the type of the event</typeparam>
    public void RegisterEvent<TEvent>() where TEvent : class, IEvent
    {
        var tEvent = typeof(TEvent);
        RemoteMap[tEvent] = this;
        Channel ??= GrpcChannel.ForAddress(RemoteAddress, ChannelOptions);
        ExecutorMap[tEvent] = new EventPublisher<TEvent>(Channel);
    }

    internal Task PublishEvent(IEvent evnt, Type tEvent, CallOptions opts)
        => !ExecutorMap.TryGetValue(tEvent, out var publisher)
               ? throw new InvalidOperationException($"No remote handler has been mapped for the event: [{tEvent.FullName}]")
               : ((IEventPublisher)publisher).PublishEvent(evnt, opts);
}