using FastEndpoints.Messaging.Remote.Core;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

sealed class EventSubscriberRuntime<TEvent, TEventHandler, TStorageRecord, TStorageProvider>
    where TEvent : class, IEvent
    where TEventHandler : class, IEventHandler<TEvent>
    where TStorageRecord : class, IEventStorageRecord, new()
    where TStorageProvider : IEventSubscriberStorageProvider<TStorageRecord>
{
    readonly TStorageProvider _storage;
    readonly IServiceProvider _serviceProvider;
    readonly ObjectFactory _handlerFactory;

    SemaphoreSlim Signal { get; } = new(0);
    SubscriberStorageBehavior StorageBehavior { get; }
    SubscriberExceptionReceiver? Errors { get; }
    TimeSpan EventRecordExpiry { get; }

    internal EventSubscriberRuntime(TStorageProvider storage, IServiceProvider serviceProvider)
    {
        _storage = storage;
        _serviceProvider = serviceProvider;
        StorageBehavior = SubscriberStorageBehavior.For(storage);
        EventSubscriberStorage<TStorageRecord, TStorageProvider>.Provider = storage;
        EventSubscriberStorage<TStorageRecord, TStorageProvider>.IsInMemProvider = storage is InMemoryEventSubscriberStorage;
        _handlerFactory = ActivatorUtilities.CreateFactory(typeof(TEventHandler), Type.EmptyTypes);
        Errors = serviceProvider.GetService<SubscriberExceptionReceiver>();
        EventRecordExpiry = RemoteConnectionCore.EventRecordExpiry;
    }

    internal Task RunReceiverAsync(CallOptions opts, CallInvoker invoker, Method<string, TEvent> method, string subscriberID, ILogger logger)
        => EventReceiverWorker.RunAsync<TEvent, TStorageRecord, TStorageProvider>(
            _storage,
            StorageBehavior,
            Signal,
            opts,
            invoker,
            method,
            subscriberID,
            typeof(TEvent).FullName!,
            EventRecordExpiry,
            logger,
            Errors);

    internal Task RunDeliveryAckReceiverAsync(CallOptions opts,
                                             CallInvoker invoker,
                                             Method<EventDeliveryAck, EventDelivery<TEvent>> method,
                                             string subscriberID,
                                             ILogger logger)
        => EventDeliveryAckReceiver.RunAsync<TEvent, TStorageRecord, TStorageProvider>(
            _storage,
            StorageBehavior,
            Signal,
            opts,
            invoker,
            method,
            subscriberID,
            typeof(TEvent).FullName!,
            EventRecordExpiry,
            logger,
            Errors);

    internal Task RunExecutorAsync(CallOptions opts, string subscriberID, ILogger logger)
        => EventExecutorWorker.RunAsync<TEvent, TEventHandler, TStorageRecord, TStorageProvider>(
            _storage,
            StorageBehavior,
            Signal,
            opts,
            Environment.ProcessorCount,
            subscriberID,
            typeof(TEvent).FullName!,
            logger,
            _handlerFactory,
            _serviceProvider,
            Errors);
}
