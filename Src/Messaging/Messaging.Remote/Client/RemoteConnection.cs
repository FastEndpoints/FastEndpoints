using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// represents a connection to a remote server that hosts command and event handlers
/// </summary>
public sealed class RemoteConnection : RemoteConnectionCore
{
    internal static Type StorageRecordType { private get; set; } = typeof(InMemoryEventStorageRecord);
    internal static Type StorageProviderType { private get; set; } = typeof(InMemoryEventSubscriberStorage);

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
    /// subscribe to a broadcast channel for a given event type (<typeparamref name="TEvent" />) on the remote host.
    /// the received events will be handled by the specified handler (<typeparamref name="TEventHandler" />) on this machine.
    /// </summary>
    /// <typeparam name="TEvent">the type of the events that will be received</typeparam>
    /// <typeparam name="TEventHandler">the handler that will be handling the received events</typeparam>
    /// <param name="callOptions">the call options</param>
    public void Subscribe<TEvent, TEventHandler>(CallOptions callOptions = default) where TEvent : class, IEvent where TEventHandler : IEventHandler<TEvent>
    {
        var tEventHandler = typeof(TEventHandler);
        RemoteMap[tEventHandler] = this;
        Channel ??= GrpcChannel.ForAddress(RemoteAddress, ChannelOptions);

        var tHandler = ServiceProvider.GetService<IEventHandler<TEvent>>()?.GetType() ?? typeof(TEventHandler);

        var tEventSubscriber = typeof(EventSubscriber<,,,>).MakeGenericType(
            typeof(TEvent),
            tHandler,
            StorageRecordType,
            StorageProviderType);

        var eventSubscriber = (ICommandExecutor)ActivatorUtilities.CreateInstance(ServiceProvider, tEventSubscriber, Channel);
        ExecutorMap[tEventHandler] = eventSubscriber;
        ((IEventSubscriber)eventSubscriber).Start(callOptions);
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