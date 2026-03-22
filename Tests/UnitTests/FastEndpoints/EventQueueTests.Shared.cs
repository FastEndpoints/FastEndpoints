using System.Reflection;
using FastEndpoints;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventQueue;

public partial class EventQueueTests
{
    static readonly TimeSpan _defaultRecordExpiry = TimeSpan.FromMinutes(1);
    static readonly ObjectFactory _unusedHandlerFactory = static (_, _) => throw new InvalidOperationException("Handler factory should not be used in this test.");

    static ILogger<EventSubscriber<TEvent, THandler, TStorageRecord, TStorageProvider>>
        GetSubscriberLogger<TEvent, THandler, TStorageRecord, TStorageProvider>(IServiceProvider provider)
        where TEvent : class, IEvent
        where THandler : class, IEventHandler<TEvent>
        where TStorageRecord : class, IEventStorageRecord, new()
        where TStorageProvider : class, IEventSubscriberStorageProvider<TStorageRecord>
        => provider.GetRequiredService<ILogger<EventSubscriber<TEvent, THandler, TStorageRecord, TStorageProvider>>>();

    static InMemoryEventStorageRecord CreateInMemoryRecord(string subscriberId,
                                                           object @event,
                                                           string? eventType = null,
                                                           DateTime? expireOn = null)
        => new()
        {
            TrackingID = Guid.NewGuid(),
            SubscriberID = subscriberId,
            EventType = eventType ?? @event.GetType().FullName ?? @event.GetType().Name,
            Event = @event,
            ExpireOn = expireOn ?? DateTime.UtcNow.Add(_defaultRecordExpiry)
        };

    static TestEventRecord CreateTestRecord<TEvent>(string subscriberId,
                                                    TEvent @event,
                                                    DateTime? expireOn = null) where TEvent : class, IEvent
        => new()
        {
            TrackingID = Guid.NewGuid(),
            SubscriberID = subscriberId,
            EventType = typeof(TEvent).FullName!,
            Event = @event,
            ExpireOn = expireOn ?? DateTime.UtcNow.Add(_defaultRecordExpiry)
        };

    static ValueTask StoreTestEventAsync<TEvent>(IEventSubscriberStorageProvider<TestEventRecord> storage,
                                                 string subscriberId,
                                                 TEvent @event,
                                                 CancellationToken cancellationToken) where TEvent : class, IEvent
        => storage.StoreEventAsync(CreateTestRecord(subscriberId, @event), cancellationToken);

    static Method<string, TEvent> CreateSubscriptionMethod<TEvent>() where TEvent : class, IEvent
        => new(
            MethodType.ServerStreaming,
            typeof(TEvent).FullName!,
            "sub",
            new MessagePackMarshaller<string>(),
            new MessagePackMarshaller<TEvent>());

    static void SetIsInMemoryProvider(EventHubBase hub, bool value = true)
        => typeof(EventHubBase)
           .GetProperty("IsInMemoryProvider", BindingFlags.NonPublic | BindingFlags.Instance)!
           .SetValue(hub, value);

    static TStorageProvider GetStaticHubStorage<TEvent, TStorageRecord, TStorageProvider>()
        where TEvent : class, IEvent
        where TStorageRecord : class, IEventStorageRecord, new()
        where TStorageProvider : class, IEventHubStorageProvider<TStorageRecord>
        => (TStorageProvider)typeof(EventHub<TEvent, TStorageRecord, TStorageProvider>)
                             .GetField("_storage", BindingFlags.NonPublic | BindingFlags.Static)!
                             .GetValue(null)!;

    static bool SubscriberExists<TEvent>(string subscriberId) where TEvent : class, IEvent
        => TryGetInMemorySubscriber(typeof(TEvent), subscriberId, out _);

    static int GetSubscriberConnectionCount<TEvent>(string subscriberId) where TEvent : class, IEvent
    {
        TryGetInMemorySubscriber(typeof(TEvent), subscriberId, out var subscriber).ShouldBeTrue();

        return (int)subscriber!.GetType().GetProperty("ConnectionCount", BindingFlags.Instance | BindingFlags.Public)!.GetValue(subscriber)!;
    }

    static bool SubscriberIsConnected<TEvent>(string subscriberId) where TEvent : class, IEvent
    {
        TryGetInMemorySubscriber(typeof(TEvent), subscriberId, out var subscriber).ShouldBeTrue();

        return (bool)subscriber!.GetType().GetProperty("IsConnected", BindingFlags.Instance | BindingFlags.Public)!.GetValue(subscriber)!;
    }

    static void SetSubscriberLastSeen<TEvent>(string subscriberId, DateTime lastSeenUtc, int connectionCount = 0) where TEvent : class, IEvent
    {
        TryGetInMemorySubscriber(typeof(TEvent), subscriberId, out var subscriber).ShouldBeTrue();
        subscriber!.GetType().GetProperty("LastSeenUtc", BindingFlags.Instance | BindingFlags.Public)!.SetValue(subscriber, lastSeenUtc);
        subscriber.GetType().GetProperty("ConnectionCount", BindingFlags.Instance | BindingFlags.Public)!.SetValue(subscriber, connectionCount);
    }

    static bool TryGetInMemorySubscriber(Type eventType, string subscriberId, out object? subscriber)
    {
        var hubType = typeof(EventHub<,,>).MakeGenericType(eventType, typeof(InMemoryEventStorageRecord), typeof(InMemoryEventHubStorage));

        return TryGetSubscriberFromHub(hubType, subscriberId, out subscriber);
    }

    static bool TryGetSubscriberFromHub(Type hubType, string subscriberId, out object? subscriber)
    {
        var registryField = hubType.GetField("_registry", BindingFlags.NonPublic | BindingFlags.Static)!;
        var registry = registryField.GetValue(null)!;
        var subscribersField = registry.GetType().GetField("Subscribers", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dictionary = subscribersField.GetValue(registry)!;
        var args = new object?[] { subscriberId, null };
        var found = (bool)dictionary.GetType().GetMethod("TryGetValue")!.Invoke(dictionary, args)!;
        subscriber = args[1];

        return found;
    }

    static string GetEventSubscriberID(object subscriber)
        => (string)subscriber.GetType().GetField("_subscriberID", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(subscriber)!;
}