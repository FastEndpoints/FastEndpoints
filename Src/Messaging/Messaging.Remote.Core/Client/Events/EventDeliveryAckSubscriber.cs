using FastEndpoints.Messaging.Remote.Core;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

sealed class EventDeliveryAckSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider> : BaseCommandExecutor<EventDeliveryAck, EventDelivery<TEvent>>,
                                                                                                   ICommandExecutor, IEventSubscriber
    where TEvent : class, IEvent
    where TEventHandler : class, IEventHandler<TEvent>
    where TStorageRecord : class, IEventStorageRecord, new()
    where TStorageProvider : IEventSubscriberStorageProvider<TStorageRecord>
{
    static readonly string _eventTypeName = typeof(TEvent).FullName!;
    static TStorageProvider? _storage;

    readonly EventSubscriberRuntime<TEvent, TEventHandler, TStorageRecord, TStorageProvider> _runtime;
    readonly ILogger<EventDeliveryAckSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider>> _logger;
    readonly string _subscriberID;

    public EventDeliveryAckSubscriber(ChannelBase channel, string clientIdentifier, IServiceProvider serviceProvider, IRpcMarshallerFactory marshaller)
        : this(channel, clientIdentifier, null, serviceProvider, marshaller) { }

    public EventDeliveryAckSubscriber(ChannelBase channel,
                                      string clientIdentifier,
                                      string? subscriberID,
                                      IServiceProvider serviceProvider,
                                      IRpcMarshallerFactory marshaller)
        : base(channel: channel, methodType: MethodType.DuplexStreaming, marshaller: marshaller, endpointName: $"{_eventTypeName}/sub-ack")
    {
        // hash EventSubscriber<...> so opting into delivery ack keeps the default id and the previous inbox.
        _subscriberID = SubscriberIDFactory.Create(
            subscriberID,
            clientIdentifier,
            typeof(EventSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider>),
            channel.Target);
        _storage ??= (TStorageProvider)ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, typeof(TStorageProvider));
        _ = EventSubscriberRetentionPolicy<TStorageRecord>.GetClockSkewAllowance(_storage);

        _runtime = new(_storage, serviceProvider);
        _logger = serviceProvider.GetRequiredService<ILogger<EventDeliveryAckSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider>>>();
        _logger.SubscriberRegistered(_subscriberID, typeof(TEventHandler).FullName!, _eventTypeName);
    }

    public void Start(CallOptions opts)
    {
        _ = _runtime.RunDeliveryAckReceiverAsync(opts, Invoker, Method, _subscriberID, _logger);
        _ = _runtime.RunExecutorAsync(opts, _subscriberID, _logger);
    }
}