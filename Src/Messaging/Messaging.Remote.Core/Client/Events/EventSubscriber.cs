using FastEndpoints.Messaging.Remote.Core;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

interface IEventSubscriber
{
    void Start(CallOptions opts);
}

sealed class EventSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider> : BaseCommandExecutor<string, TEvent>, ICommandExecutor, IEventSubscriber
    where TEvent : class, IEvent
    where TEventHandler : class, IEventHandler<TEvent>
    where TStorageRecord : class, IEventStorageRecord, new()
    where TStorageProvider : IEventSubscriberStorageProvider<TStorageRecord>
{
    static readonly string _eventTypeName = typeof(TEvent).FullName!;
    static TStorageProvider? _storage;

    readonly EventSubscriberRuntime<TEvent, TEventHandler, TStorageRecord, TStorageProvider> _runtime;
    readonly ILogger<EventSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider>> _logger;
    readonly string _subscriberID;

    public EventSubscriber(ChannelBase channel, string clientIdentifier, IServiceProvider serviceProvider, IRpcMarshallerFactory marshaller)
        : this(channel, clientIdentifier, null, serviceProvider, marshaller) { }

    public EventSubscriber(ChannelBase channel, string clientIdentifier, string? subscriberID, IServiceProvider serviceProvider, IRpcMarshallerFactory marshaller)
        : base(channel: channel, methodType: MethodType.ServerStreaming, marshaller: marshaller, endpointName: $"{_eventTypeName}/sub")
    {
        _subscriberID = SubscriberIDFactory.Create(subscriberID, clientIdentifier, GetType(), channel.Target);
        _storage ??= (TStorageProvider)ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, typeof(TStorageProvider));

        _runtime = new(_storage, serviceProvider);
        _logger = serviceProvider.GetRequiredService<ILogger<EventSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider>>>();
        _logger.SubscriberRegistered(_subscriberID, typeof(TEventHandler).FullName!, _eventTypeName);
    }

    public void Start(CallOptions opts)
    {
        _ = _runtime.RunReceiverAsync(opts, Invoker, Method, _subscriberID, _logger);
        _ = _runtime.RunExecutorAsync(opts, _subscriberID, _logger);
    }
}