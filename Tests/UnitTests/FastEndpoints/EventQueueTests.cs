using System.Reflection;
using FakeItEasy;
using FastEndpoints;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EventQueue;

public class EventQueueTests
{
    [Fact]
    public async Task subscriber_storage_queue_and_dequeue()
    {
        var sut = new InMemoryEventSubscriberStorage();

        var record1 = new InMemoryEventStorageRecord
        {
            Event = "test1",
            EventType = "System.String",
            ExpireOn = DateTime.UtcNow.AddMinutes(1),
            SubscriberID = "sub1"
        };

        var record2 = new InMemoryEventStorageRecord
        {
            Event = "test2",
            EventType = "System.String",
            ExpireOn = DateTime.UtcNow.AddMinutes(1),
            SubscriberID = "sub1"
        };

        var record3 = new InMemoryEventStorageRecord
        {
            Event = "test3",
            EventType = "System.String",
            ExpireOn = DateTime.UtcNow.AddMinutes(1),
            SubscriberID = "sub2"
        };

        await sut.StoreEventAsync(record1, default);
        await sut.StoreEventAsync(record2, default);

        var r1x = await sut.GetNextBatchAsync(new() { SubscriberID = record1.SubscriberID });
        r1x!.Single().Event.ShouldBe(record1.Event);

        var r2x = await sut.GetNextBatchAsync(new() { SubscriberID = record1.SubscriberID });
        r2x!.Single().Event.ShouldBe(record2.Event);

        await sut.StoreEventAsync(record3, default);
        await Task.Delay(100);

        var r3 = await sut.GetNextBatchAsync(new() { SubscriberID = record3.SubscriberID });
        r3!.Single().Event.ShouldBe(record3.Event);

        var r3x = await sut.GetNextBatchAsync(new() { SubscriberID = record2.SubscriberID });
        r3x.Any().ShouldBeFalse();

        var r4x = await sut.GetNextBatchAsync(new() { SubscriberID = record3.SubscriberID });
        r4x.Any().ShouldBeFalse();
    }

    [Fact]
    public async Task subscriber_storage_queue_overflow()
    {
        var sut = new InMemoryEventSubscriberStorage();

        InMemoryEventQueue.MaxLimit = 10000;

        for (var i = 0; i <= InMemoryEventQueue.MaxLimit; i++)
        {
            var r = new InMemoryEventStorageRecord
            {
                Event = i,
                EventType = "System.String",
                ExpireOn = DateTime.UtcNow.AddMinutes(1),
                SubscriberID = "subId"
            };

            if (i < InMemoryEventQueue.MaxLimit)
                await sut.StoreEventAsync(r, default);
            else
            {
                var func = async () => await sut.StoreEventAsync(r, default);
                await func.ShouldThrowAsync<OverflowException>();
            }
        }
    }

    [Fact]
    public async Task event_hub_publisher_mode()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(A.Fake<IHostApplicationLifetime>());
        var provider = services.BuildServiceProvider();
        var hub = new EventHub<TestEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        EventHub<TestEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.EventPublisher;

        var writer = new TestServerStreamWriter<TestEvent>();

        var ctx = A.Fake<ServerCallContext>();
        A.CallTo(ctx).WithReturnType<CancellationToken>().Returns(default);

        _ = hub.OnSubscriberConnected(hub, Guid.NewGuid().ToString(), writer, ctx);
        _ = hub.OnSubscriberConnected(hub, Guid.NewGuid().ToString(), writer, ctx);

        var e1 = new TestEvent { EventID = 123 };
        EventHubBase.AddToSubscriberQueues(e1);

        while (writer.Responses.Count < 1)
            await Task.Delay(100);

        writer.Responses[0].EventID.ShouldBe(123);
    }

    [Fact]
    public async Task event_hub_broker_mode()
    {
        var provider = CreateServiceProvider();
        var hub = new EventHub<TestEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        EventHub<TestEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.EventBroker;

        var writer = new TestServerStreamWriter<TestEvent>();

        var ctx = A.Fake<ServerCallContext>();
        A.CallTo(ctx).WithReturnType<CancellationToken>().Returns(default);

        _ = hub.OnSubscriberConnected(hub, Guid.NewGuid().ToString(), writer, ctx);

        var e1 = new TestEvent { EventID = 321 };
        _ = hub.OnEventReceived(hub, e1, ctx);

        while (writer.Responses.Count < 1)
            await Task.Delay(100);

        writer.Responses[0].EventID.ShouldBe(321);
    }

    [Fact]
    public void explicit_subscriber_id_overrides_derived_identifier()
    {
        var provider = CreateServiceProvider();
        using var channel = GrpcChannel.ForAddress("http://localhost:5001");
        var subscriber = new EventSubscriber<ExplicitSubscriberIdEvent, ExplicitSubscriberIdHandler, InMemoryEventStorageRecord, InMemoryEventSubscriberStorage>(
            channel,
            clientIdentifier: "client-a",
            subscriberID: "known-sub-1",
            serviceProvider: provider);

        GetEventSubscriberID(subscriber).ShouldBe("known-sub-1");
    }

    [Fact]
    public void subscriber_id_is_derived_when_explicit_id_is_not_supplied()
    {
        var provider = CreateServiceProvider();
        using var channel = GrpcChannel.ForAddress("http://localhost:5002");
        var subscriber = new EventSubscriber<DerivedSubscriberIdEvent, DerivedSubscriberIdHandler, InMemoryEventStorageRecord, InMemoryEventSubscriberStorage>(
            channel,
            clientIdentifier: "client-b",
            subscriberID: null,
            serviceProvider: provider);

        var expected = SubscriberIDFactory.Create(null, "client-b", subscriber.GetType(), channel.Target);

        GetEventSubscriberID(subscriber).ShouldBe(expected);
    }

    [Fact]
    public async Task known_subscriber_receives_events_published_before_first_connection()
    {
        var provider = CreateServiceProvider();
        EventHub<KnownSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Configure(HubMode.EventPublisher, ["known-sub-2"]);
        var hub = new EventHub<KnownSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);

        EventHubBase.AddToSubscriberQueues(new KnownSubscriberEvent { EventID = 777 });

        var writer = new TestServerStreamWriter<KnownSubscriberEvent>();
        using var cts = new CancellationTokenSource();
        var task = hub.OnSubscriberConnected(hub, "known-sub-2", writer, CreateServerCallContext(cts.Token));

        await WaitUntil(() => writer.Responses.Count == 1);

        writer.Responses.Single().EventID.ShouldBe(777);

        cts.Cancel();
        await WaitForCompletion(task);
    }

    [Fact]
    public async Task configured_subscriber_is_not_pruned_after_24_hours_and_receives_new_events()
    {
        var provider = CreateServiceProvider();
        EventHub<ConfiguredSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Configure(HubMode.EventPublisher, ["known-sub-3"]);
        var hub = new EventHub<ConfiguredSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);

        SetSubscriberLastSeen<ConfiguredSubscriberEvent>("known-sub-3", DateTime.UtcNow.AddHours(-25));

        EventHubBase.AddToSubscriberQueues(new ConfiguredSubscriberEvent { EventID = 888 });

        SubscriberExists<ConfiguredSubscriberEvent>("known-sub-3").ShouldBeTrue();

        var writer = new TestServerStreamWriter<ConfiguredSubscriberEvent>();
        using var cts = new CancellationTokenSource();
        var task = hub.OnSubscriberConnected(hub, "known-sub-3", writer, CreateServerCallContext(cts.Token));

        await WaitUntil(() => writer.Responses.Count == 1);

        writer.Responses.Single().EventID.ShouldBe(888);

        cts.Cancel();
        await WaitForCompletion(task);
    }

    [Fact]
    public async Task disconnected_subscriber_receives_events_when_reconnecting_within_24_hours()
    {
        var provider = CreateServiceProvider();
        EventHub<ReconnectWindowEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.EventPublisher;
        var hub = new EventHub<ReconnectWindowEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);

        var subscriberId = Guid.NewGuid().ToString();
        var initialWriter = new TestServerStreamWriter<ReconnectWindowEvent>();
        using var initialCts = new CancellationTokenSource();
        var initialTask = hub.OnSubscriberConnected(hub, subscriberId, initialWriter, CreateServerCallContext(initialCts.Token));

        await WaitUntil(() => SubscriberExists<ReconnectWindowEvent>(subscriberId));

        initialCts.Cancel();
        await WaitForCompletion(initialTask);

        EventHubBase.AddToSubscriberQueues(new ReconnectWindowEvent { EventID = 123 });

        var reconnectWriter = new TestServerStreamWriter<ReconnectWindowEvent>();
        using var reconnectCts = new CancellationTokenSource();
        var reconnectTask = hub.OnSubscriberConnected(hub, subscriberId, reconnectWriter, CreateServerCallContext(reconnectCts.Token));

        await WaitUntil(() => reconnectWriter.Responses.Count == 1);

        reconnectWriter.Responses.Single().EventID.ShouldBe(123);

        reconnectCts.Cancel();
        await WaitForCompletion(reconnectTask);
    }

    [Fact]
    public async Task stale_disconnected_subscriber_is_pruned_and_stops_receiving_new_events()
    {
        var provider = CreateServiceProvider();
        EventHub<StaleSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.EventPublisher;
        var hub = new EventHub<StaleSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);

        var staleSubscriberId = Guid.NewGuid().ToString();
        var staleWriter = new TestServerStreamWriter<StaleSubscriberEvent>();
        using var staleCts = new CancellationTokenSource();
        var staleTask = hub.OnSubscriberConnected(hub, staleSubscriberId, staleWriter, CreateServerCallContext(staleCts.Token));

        await WaitUntil(() => SubscriberExists<StaleSubscriberEvent>(staleSubscriberId));

        staleCts.Cancel();
        await WaitForCompletion(staleTask);

        var activeSubscriberId = Guid.NewGuid().ToString();
        var activeWriter = new TestServerStreamWriter<StaleSubscriberEvent>();
        using var activeCts = new CancellationTokenSource();
        var activeTask = hub.OnSubscriberConnected(hub, activeSubscriberId, activeWriter, CreateServerCallContext(activeCts.Token));

        await WaitUntil(() => SubscriberExists<StaleSubscriberEvent>(activeSubscriberId));

        SetSubscriberLastSeen<StaleSubscriberEvent>(staleSubscriberId, DateTime.UtcNow.AddHours(-25));

        EventHubBase.AddToSubscriberQueues(new StaleSubscriberEvent { EventID = 456 });

        await WaitUntil(() => activeWriter.Responses.Count == 1);

        activeWriter.Responses.Single().EventID.ShouldBe(456);
        SubscriberExists<StaleSubscriberEvent>(staleSubscriberId).ShouldBeFalse();

        var prunedReconnectWriter = new TestServerStreamWriter<StaleSubscriberEvent>();
        using var prunedReconnectCts = new CancellationTokenSource();
        var prunedReconnectTask = hub.OnSubscriberConnected(hub, staleSubscriberId, prunedReconnectWriter, CreateServerCallContext(prunedReconnectCts.Token));

        await Task.Delay(300);

        prunedReconnectWriter.Responses.ShouldBeEmpty();

        prunedReconnectCts.Cancel();
        activeCts.Cancel();

        await WaitForCompletion(prunedReconnectTask);
        await WaitForCompletion(activeTask);
    }

    static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(A.Fake<IHostApplicationLifetime>());

        return services.BuildServiceProvider();
    }

    static ServerCallContext CreateServerCallContext(CancellationToken ct)
    {
        var ctx = A.Fake<ServerCallContext>();
        A.CallTo(ctx).WithReturnType<CancellationToken>().Returns(ct);

        return ctx;
    }

    static bool SubscriberExists<TEvent>(string subscriberId) where TEvent : class, IEvent
        => TryGetSubscriber(typeof(TEvent), subscriberId, out _);

    static void SetSubscriberLastSeen<TEvent>(string subscriberId, DateTime lastSeenUtc) where TEvent : class, IEvent
    {
        TryGetSubscriber(typeof(TEvent), subscriberId, out var subscriber).ShouldBeTrue();
        subscriber!.GetType().GetProperty("LastSeenUtc", BindingFlags.Instance | BindingFlags.Public)!.SetValue(subscriber, lastSeenUtc);
        subscriber.GetType().GetProperty("IsConnected", BindingFlags.Instance | BindingFlags.Public)!.SetValue(subscriber, false);
    }

    static bool TryGetSubscriber(Type eventType, string subscriberId, out object? subscriber)
    {
        var hubType = typeof(EventHub<,,>).MakeGenericType(eventType, typeof(InMemoryEventStorageRecord), typeof(InMemoryEventHubStorage));
        var field = hubType.GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Static)!;
        var dict = field.GetValue(null)!;
        var args = new object?[] { subscriberId, null };
        var found = (bool)dict.GetType().GetMethod("TryGetValue")!.Invoke(dict, args)!;
        subscriber = args[1];

        return found;
    }

    static string GetEventSubscriberID(object subscriber)
        => (string)subscriber.GetType().GetField("_subscriberID", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(subscriber)!;

    static async Task WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var timeoutAt = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < timeoutAt)
        {
            if (condition())
                return;

            await Task.Delay(50);
        }

        condition().ShouldBeTrue();
    }

    static async Task WaitForCompletion(Task task, int timeoutMs = 3000)
    {
        await Task.WhenAny(task, Task.Delay(timeoutMs));
        task.IsCompleted.ShouldBeTrue();
        await task;
    }

    class TestEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class ReconnectWindowEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class ExplicitSubscriberIdEvent : IEvent;

    class ExplicitSubscriberIdHandler : IEventHandler<ExplicitSubscriberIdEvent>
    {
        public Task HandleAsync(ExplicitSubscriberIdEvent evnt, CancellationToken ct)
            => Task.CompletedTask;
    }

    class DerivedSubscriberIdEvent : IEvent;

    class DerivedSubscriberIdHandler : IEventHandler<DerivedSubscriberIdEvent>
    {
        public Task HandleAsync(DerivedSubscriberIdEvent evnt, CancellationToken ct)
            => Task.CompletedTask;
    }

    class KnownSubscriberEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class ConfiguredSubscriberEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class StaleSubscriberEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class TestServerStreamWriter<T> : IServerStreamWriter<T>
    {
        public WriteOptions? WriteOptions { get; set; }
        public List<T> Responses { get; } = new();

        public async Task WriteAsync(T message)
            => Responses.Add(message);

        public Task WriteAsync(T message, CancellationToken ct)
            => WriteAsync(message);
    }
}