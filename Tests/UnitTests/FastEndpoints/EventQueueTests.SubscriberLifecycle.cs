using FastEndpoints;
using Grpc.Net.Client;
using QueueTesting;
using Xunit;
using static QueueTesting.QueueTestSupport;

namespace EventQueue;

public partial class EventQueueTests
{
    [Fact]
    public async Task explicit_subscriber_ids_can_be_reused_across_event_types_without_cross_delivery()
    {
        const string subscriberId = "shared-known-sub";
        var provider = CreateServiceProvider();

        EventHub<KnownSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Configure(HubMode.EventPublisher, [subscriberId]);
        EventHub<ConfiguredSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Configure(HubMode.EventPublisher, [subscriberId]);

        var knownHub = new EventHub<KnownSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        var configuredHub = new EventHub<ConfiguredSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        var knownWriter = new TestServerStreamWriter<KnownSubscriberEvent>();
        var configuredWriter = new TestServerStreamWriter<ConfiguredSubscriberEvent>();
        using var knownCts = new CancellationTokenSource();
        using var configuredCts = new CancellationTokenSource();

        EventHubBase.AddToSubscriberQueues(new KnownSubscriberEvent { EventID = 111 });
        EventHubBase.AddToSubscriberQueues(new ConfiguredSubscriberEvent { EventID = 222 });

        var knownTask = knownHub.OnSubscriberConnected(knownHub, subscriberId, knownWriter, CreateServerCallContext(knownCts.Token));
        var configuredTask = configuredHub.OnSubscriberConnected(configuredHub, subscriberId, configuredWriter, CreateServerCallContext(configuredCts.Token));

        await WaitUntil(() => knownWriter.Responses.Count == 1 && configuredWriter.Responses.Count == 1);

        knownWriter.Responses.Single().EventID.ShouldBe(111);
        configuredWriter.Responses.Single().EventID.ShouldBe(222);

        knownCts.Cancel();
        configuredCts.Cancel();

        await WaitForCompletion(knownTask);
        await WaitForCompletion(configuredTask);
    }

    [Fact]
    public void explicit_subscriber_id_overrides_the_derived_identifier()
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
    public void subscriber_id_is_derived_when_no_explicit_id_is_supplied()
    {
        var provider = CreateServiceProvider();
        using var channel = GrpcChannel.ForAddress("http://localhost:5002");

        var subscriber = new EventSubscriber<DerivedSubscriberIdEvent, DerivedSubscriberIdHandler, InMemoryEventStorageRecord, InMemoryEventSubscriberStorage>(
            channel,
            clientIdentifier: "client-b",
            subscriberID: null,
            serviceProvider: provider);

        var expectedSubscriberId = SubscriberIDFactory.Create(null, "client-b", subscriber.GetType(), channel.Target);

        GetEventSubscriberID(subscriber).ShouldBe(expectedSubscriberId);
    }

    [Fact]
    public async Task known_subscriber_receives_events_published_before_the_first_connection()
    {
        var provider = CreateServiceProvider();
        EventHub<KnownSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Configure(HubMode.EventPublisher, ["known-sub-2"]);

        var hub = new EventHub<KnownSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        var writer = new TestServerStreamWriter<KnownSubscriberEvent>();
        using var cts = new CancellationTokenSource();

        EventHubBase.AddToSubscriberQueues(new KnownSubscriberEvent { EventID = 777 });

        var subscriberTask = hub.OnSubscriberConnected(hub, "known-sub-2", writer, CreateServerCallContext(cts.Token));

        await WaitUntil(() => writer.Responses.Count == 1);
        writer.Responses.Single().EventID.ShouldBe(777);

        cts.Cancel();
        await WaitForCompletion(subscriberTask);
    }

    [Fact]
    public async Task configured_subscriber_is_not_pruned_after_24_hours_and_receives_new_events()
    {
        var provider = CreateServiceProvider();
        EventHub<ConfiguredSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Configure(HubMode.EventPublisher, ["known-sub-3"]);

        var hub = new EventHub<ConfiguredSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        var writer = new TestServerStreamWriter<ConfiguredSubscriberEvent>();
        using var cts = new CancellationTokenSource();

        SetSubscriberLastSeen<ConfiguredSubscriberEvent>("known-sub-3", DateTime.UtcNow.AddHours(-25));
        EventHubBase.AddToSubscriberQueues(new ConfiguredSubscriberEvent { EventID = 888 });

        SubscriberExists<ConfiguredSubscriberEvent>("known-sub-3").ShouldBeTrue();

        var subscriberTask = hub.OnSubscriberConnected(hub, "known-sub-3", writer, CreateServerCallContext(cts.Token));

        await WaitUntil(() => writer.Responses.Count == 1);
        writer.Responses.Single().EventID.ShouldBe(888);

        cts.Cancel();
        await WaitForCompletion(subscriberTask);
    }

    [Fact]
    public async Task disconnected_subscriber_receives_missed_events_when_reconnecting_within_24_hours()
    {
        var provider = CreateServiceProvider();
        EventHub<ReconnectWindowEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.EventPublisher;

        var hub = new EventHub<ReconnectWindowEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        var subscriberId = Guid.NewGuid().ToString();
        var firstWriter = new TestServerStreamWriter<ReconnectWindowEvent>();
        using var firstCts = new CancellationTokenSource();

        var firstConnection = hub.OnSubscriberConnected(hub, subscriberId, firstWriter, CreateServerCallContext(firstCts.Token));

        await WaitUntil(() => SubscriberExists<ReconnectWindowEvent>(subscriberId));

        firstCts.Cancel();
        await WaitForCompletion(firstConnection);

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
    public async Task stale_disconnected_subscriber_is_pruned_and_no_longer_receives_new_events()
    {
        var provider = CreateServiceProvider();
        EventHub<StaleSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.EventPublisher;

        var hub = new EventHub<StaleSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        var staleSubscriberId = Guid.NewGuid().ToString();
        var activeSubscriberId = Guid.NewGuid().ToString();
        var staleWriter = new TestServerStreamWriter<StaleSubscriberEvent>();
        var activeWriter = new TestServerStreamWriter<StaleSubscriberEvent>();
        using var staleCts = new CancellationTokenSource();
        using var activeCts = new CancellationTokenSource();

        var staleTask = hub.OnSubscriberConnected(hub, staleSubscriberId, staleWriter, CreateServerCallContext(staleCts.Token));

        await WaitUntil(() => SubscriberExists<StaleSubscriberEvent>(staleSubscriberId));

        staleCts.Cancel();
        await WaitForCompletion(staleTask);

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

    [Fact]
    public async Task overlapping_round_robin_reconnect_keeps_subscriber_eligible_after_older_connection_disconnects()
    {
        var provider = CreateServiceProvider();
        EventHub<RoundRobinReconnectRaceEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.RoundRobin;

        var hub = new EventHub<RoundRobinReconnectRaceEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        var sharedSubscriberId = Guid.NewGuid().ToString();
        var otherSubscriberId = Guid.NewGuid().ToString();
        var initialWriter = new TestServerStreamWriter<RoundRobinReconnectRaceEvent>();
        var reconnectWriter = new TestServerStreamWriter<RoundRobinReconnectRaceEvent>();
        var otherWriter = new TestServerStreamWriter<RoundRobinReconnectRaceEvent>();
        using var initialCts = new CancellationTokenSource();
        using var reconnectCts = new CancellationTokenSource();
        using var otherCts = new CancellationTokenSource();

        var initialTask = hub.OnSubscriberConnected(hub, sharedSubscriberId, initialWriter, CreateServerCallContext(initialCts.Token));
        var otherTask = hub.OnSubscriberConnected(hub, otherSubscriberId, otherWriter, CreateServerCallContext(otherCts.Token));

        await WaitUntil(() => SubscriberExists<RoundRobinReconnectRaceEvent>(sharedSubscriberId) && SubscriberExists<RoundRobinReconnectRaceEvent>(otherSubscriberId));

        var reconnectTask = hub.OnSubscriberConnected(hub, sharedSubscriberId, reconnectWriter, CreateServerCallContext(reconnectCts.Token));

        await WaitUntil(() => GetSubscriberConnectionCount<RoundRobinReconnectRaceEvent>(sharedSubscriberId) == 2);

        initialCts.Cancel();
        await WaitForCompletion(initialTask);

        GetSubscriberConnectionCount<RoundRobinReconnectRaceEvent>(sharedSubscriberId).ShouldBe(1);
        SubscriberIsConnected<RoundRobinReconnectRaceEvent>(sharedSubscriberId).ShouldBeTrue();

        for (var eventId = 1; eventId <= 4; eventId++)
            EventHubBase.AddToSubscriberQueues(new RoundRobinReconnectRaceEvent { EventID = eventId });

        await WaitUntil(() => reconnectWriter.Responses.Count + otherWriter.Responses.Count == 4, timeoutMs: 5000);

        reconnectWriter.Responses.Count.ShouldBe(2);
        otherWriter.Responses.Count.ShouldBe(2);

        reconnectCts.Cancel();
        otherCts.Cancel();

        await WaitForCompletion(reconnectTask);
        await WaitForCompletion(otherTask);
    }

    [Fact]
    public async Task active_reconnect_is_not_pruned_when_an_older_connection_marks_the_subscriber_stale()
    {
        var provider = CreateServiceProvider();
        EventHub<StaleReconnectRaceEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.EventPublisher;

        var hub = new EventHub<StaleReconnectRaceEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        var sharedSubscriberId = Guid.NewGuid().ToString();
        var otherSubscriberId = Guid.NewGuid().ToString();
        var initialWriter = new TestServerStreamWriter<StaleReconnectRaceEvent>();
        var reconnectWriter = new TestServerStreamWriter<StaleReconnectRaceEvent>();
        var otherWriter = new TestServerStreamWriter<StaleReconnectRaceEvent>();
        using var initialCts = new CancellationTokenSource();
        using var reconnectCts = new CancellationTokenSource();
        using var otherCts = new CancellationTokenSource();

        var initialTask = hub.OnSubscriberConnected(hub, sharedSubscriberId, initialWriter, CreateServerCallContext(initialCts.Token));
        var reconnectTask = hub.OnSubscriberConnected(hub, sharedSubscriberId, reconnectWriter, CreateServerCallContext(reconnectCts.Token));
        var otherTask = hub.OnSubscriberConnected(hub, otherSubscriberId, otherWriter, CreateServerCallContext(otherCts.Token));

        await WaitUntil(() => GetSubscriberConnectionCount<StaleReconnectRaceEvent>(sharedSubscriberId) == 2 && SubscriberExists<StaleReconnectRaceEvent>(otherSubscriberId));

        initialCts.Cancel();
        await WaitForCompletion(initialTask);

        SetSubscriberLastSeen<StaleReconnectRaceEvent>(sharedSubscriberId, DateTime.UtcNow.AddHours(-25), connectionCount: 1);
        EventHubBase.AddToSubscriberQueues(new StaleReconnectRaceEvent { EventID = 999 });

        await WaitUntil(() => reconnectWriter.Responses.Count == 1 && otherWriter.Responses.Count == 1, timeoutMs: 5000);

        reconnectWriter.Responses.Single().EventID.ShouldBe(999);
        otherWriter.Responses.Single().EventID.ShouldBe(999);
        SubscriberExists<StaleReconnectRaceEvent>(sharedSubscriberId).ShouldBeTrue();
        GetSubscriberConnectionCount<StaleReconnectRaceEvent>(sharedSubscriberId).ShouldBe(1);
        SubscriberIsConnected<StaleReconnectRaceEvent>(sharedSubscriberId).ShouldBeTrue();

        reconnectCts.Cancel();
        otherCts.Cancel();

        await WaitForCompletion(reconnectTask);
        await WaitForCompletion(otherTask);
    }
}
