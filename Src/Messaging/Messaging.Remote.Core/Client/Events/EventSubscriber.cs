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
    static SubscriberStorageBehavior _storageBehavior = SubscriberStorageBehavior.Durable;

    readonly SemaphoreSlim _sem = new(0);
    readonly ObjectFactory _handlerFactory;
    readonly IServiceProvider _serviceProvider;
    readonly SubscriberExceptionReceiver? _errorReceiver;
    readonly ILogger<EventSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider>> _logger;
    readonly string _subscriberID;
    readonly TimeSpan _eventRecordExpiry;

    public EventSubscriber(ChannelBase channel, string clientIdentifier, IServiceProvider serviceProvider)
        : this(channel, clientIdentifier, null, serviceProvider) { }

    public EventSubscriber(ChannelBase channel, string clientIdentifier, string? subscriberID, IServiceProvider serviceProvider)
        : base(channel: channel, methodType: MethodType.ServerStreaming, endpointName: $"{_eventTypeName}/sub")
    {
        _subscriberID = SubscriberIDFactory.Create(subscriberID, clientIdentifier, GetType(), channel.Target);
        _serviceProvider = serviceProvider;
        _storage ??= (TStorageProvider)ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, typeof(TStorageProvider));
        _storageBehavior = SubscriberStorageBehavior.For(_storage);
        EventSubscriberStorage<TStorageRecord, TStorageProvider>.Provider = _storage; //setup stale record purge task
        EventSubscriberStorage<TStorageRecord, TStorageProvider>.IsInMemProvider = _storage is InMemoryEventSubscriberStorage;
        _handlerFactory = ActivatorUtilities.CreateFactory(typeof(TEventHandler), Type.EmptyTypes);
        _errorReceiver = _serviceProvider.GetService<SubscriberExceptionReceiver>();
        _eventRecordExpiry = RemoteConnectionCore.EventRecordExpiry;
        _logger = serviceProvider.GetRequiredService<ILogger<EventSubscriber<TEvent, TEventHandler, TStorageRecord, TStorageProvider>>>();
        _logger.SubscriberRegistered(_subscriberID, typeof(TEventHandler).FullName!, _eventTypeName);
    }

    public void Start(CallOptions opts)
    {
        _ = EventReceiverWorker.RunAsync<TEvent, TStorageRecord, TStorageProvider>(
            _storage!,
            _storageBehavior,
            _sem,
            opts,
            Invoker,
            Method,
            _subscriberID,
            _eventTypeName,
            _eventRecordExpiry,
            _logger,
            _errorReceiver);

        _ = EventExecutorWorker.RunAsync<TEvent, TEventHandler, TStorageRecord, TStorageProvider>(
            _storage!,
            _storageBehavior,
            _sem,
            opts,
            Environment.ProcessorCount,
            _subscriberID,
            _eventTypeName,
            _logger,
            _handlerFactory,
            _serviceProvider,
            _errorReceiver);
    }
}