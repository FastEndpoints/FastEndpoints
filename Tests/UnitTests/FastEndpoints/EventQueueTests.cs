using System.Diagnostics;
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
            TrackingID = Guid.NewGuid(),
            Event = "test1",
            EventType = "System.String",
            ExpireOn = DateTime.UtcNow.AddMinutes(1),
            SubscriberID = "sub1"
        };

        var record2 = new InMemoryEventStorageRecord
        {
            TrackingID = Guid.NewGuid(),
            Event = "test2",
            EventType = "System.String",
            ExpireOn = DateTime.UtcNow.AddMinutes(1),
            SubscriberID = "sub1"
        };

        var record3 = new InMemoryEventStorageRecord
        {
            TrackingID = Guid.NewGuid(),
            Event = "test3",
            EventType = "System.String",
            ExpireOn = DateTime.UtcNow.AddMinutes(1),
            SubscriberID = "sub2"
        };

        await sut.StoreEventAsync(record1, default);
        await sut.StoreEventAsync(record2, default);

        var r1x = await sut.GetNextBatchAsync(new() { SubscriberID = record1.SubscriberID, EventType = record1.EventType });
        r1x!.Single().Event.ShouldBe(record1.Event);

        var r2x = await sut.GetNextBatchAsync(new() { SubscriberID = record1.SubscriberID, EventType = record1.EventType });
        r2x!.Single().Event.ShouldBe(record2.Event);

        await sut.StoreEventAsync(record3, default);
        await Task.Delay(100);

        var r3 = await sut.GetNextBatchAsync(new() { SubscriberID = record3.SubscriberID, EventType = record3.EventType });
        r3!.Single().Event.ShouldBe(record3.Event);

        var r3x = await sut.GetNextBatchAsync(new() { SubscriberID = record2.SubscriberID, EventType = record2.EventType });
        r3x.Any().ShouldBeFalse();

        var r4x = await sut.GetNextBatchAsync(new() { SubscriberID = record3.SubscriberID, EventType = record3.EventType });
        r4x.Any().ShouldBeFalse();
    }

    [Fact]
    public async Task subscriber_storage_separates_reused_explicit_ids_by_event_type()
    {
        var sut = new InMemoryEventSubscriberStorage();
        const string subscriberId = "shared-sub";

        await sut.StoreEventAsync(
            new()
            {
                TrackingID = Guid.NewGuid(),
                SubscriberID = subscriberId,
                EventType = typeof(KnownSubscriberEvent).FullName!,
                Event = new KnownSubscriberEvent { EventID = 111 },
                ExpireOn = DateTime.UtcNow.AddMinutes(1)
            },
            default);

        await sut.StoreEventAsync(
            new()
            {
                TrackingID = Guid.NewGuid(),
                SubscriberID = subscriberId,
                EventType = typeof(ConfiguredSubscriberEvent).FullName!,
                Event = new ConfiguredSubscriberEvent { EventID = 222 },
                ExpireOn = DateTime.UtcNow.AddMinutes(1)
            },
            default);

        var known = await sut.GetNextBatchAsync(new() { SubscriberID = subscriberId, EventType = typeof(KnownSubscriberEvent).FullName! });
        var configured = await sut.GetNextBatchAsync(new() { SubscriberID = subscriberId, EventType = typeof(ConfiguredSubscriberEvent).FullName! });

        ((KnownSubscriberEvent)known.Single().Event).EventID.ShouldBe(111);
        ((ConfiguredSubscriberEvent)configured.Single().Event).EventID.ShouldBe(222);
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
                TrackingID = Guid.NewGuid(),
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
    public async Task event_hub_wakes_immediately_after_poll_timeout_when_new_event_arrives()
    {
        var state = new InstrumentedEventHubStorageState();
        var provider = CreateServiceProvider(s => s.AddSingleton(state));
        var hub = new EventHub<WaitRecoveryEvent, TestEventRecord, InstrumentedEventHubStorage>(provider);
        EventHub<WaitRecoveryEvent, TestEventRecord, InstrumentedEventHubStorage>.Mode = HubMode.EventPublisher;
        var writer = new TestServerStreamWriter<WaitRecoveryEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var task = hub.OnSubscriberConnected(hub, "wait-recovery-sub", writer, CreateServerCallContext(cts.Token));

        await state.SecondFetchObserved.Task.WaitAsync(TimeSpan.FromSeconds(12));
        await Task.Delay(100);

        var sw = Stopwatch.StartNew();
        await hub.BroadcastEventTaskForTesting(new() { EventID = 42 });
        await WaitUntil(() => writer.Responses.Count == 1, timeoutMs: 2000);

        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
        writer.Responses.Single().EventID.ShouldBe(42);

        cts.Cancel();
        await WaitForCompletion(task, timeoutMs: 5000);
    }

    [Fact]
    public async Task event_hub_drains_residual_semaphore_releases_after_processing_backlog()
    {
        var state = new InstrumentedEventHubStorageState();
        var provider = CreateServiceProvider(s => s.AddSingleton(state));
        var hub = new EventHub<PollDrainEvent, TestEventRecord, InstrumentedEventHubStorage>(provider);
        EventHub<PollDrainEvent, TestEventRecord, InstrumentedEventHubStorage>.Mode = HubMode.EventPublisher;
        var writer = new GateServerStreamWriter<PollDrainEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var task = hub.OnSubscriberConnected(hub, "poll-drain-sub", writer, CreateServerCallContext(cts.Token));

        const int eventCount = 100;

        for (var i = 1; i <= eventCount; i++)
            await hub.BroadcastEventTaskForTesting(new() { EventID = i });

        writer.Release();

        await WaitUntil(() => writer.Responses.Count == eventCount, timeoutMs: 5000);
        await Task.Delay(250);

        state.GetNextBatchCallCount.ShouldBeLessThan(12);

        cts.Cancel();
        await WaitForCompletion(task, timeoutMs: 5000);
    }

    [Fact]
    public async Task explicit_subscriber_id_can_be_reused_across_event_types_without_cross_delivery()
    {
        var provider = CreateServiceProvider();
        const string subscriberId = "shared-known-sub";
        EventHub<KnownSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Configure(HubMode.EventPublisher, [subscriberId]);
        EventHub<ConfiguredSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Configure(HubMode.EventPublisher, [subscriberId]);
        var knownHub = new EventHub<KnownSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        var configuredHub = new EventHub<ConfiguredSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);

        EventHubBase.AddToSubscriberQueues(new KnownSubscriberEvent { EventID = 111 });
        EventHubBase.AddToSubscriberQueues(new ConfiguredSubscriberEvent { EventID = 222 });

        var knownWriter = new TestServerStreamWriter<KnownSubscriberEvent>();
        var configuredWriter = new TestServerStreamWriter<ConfiguredSubscriberEvent>();
        using var knownCts = new CancellationTokenSource();
        using var configuredCts = new CancellationTokenSource();
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

    [Fact]
    public async Task overlapping_round_robin_reconnect_keeps_subscriber_eligible_after_older_connection_disconnects()
    {
        var provider = CreateServiceProvider();
        EventHub<RoundRobinReconnectRaceEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.RoundRobin;
        var hub = new EventHub<RoundRobinReconnectRaceEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);

        var sharedSubscriberId = Guid.NewGuid().ToString();
        var otherSubscriberId = Guid.NewGuid().ToString();

        var initialWriter = new TestServerStreamWriter<RoundRobinReconnectRaceEvent>();
        using var initialCts = new CancellationTokenSource();
        var initialTask = hub.OnSubscriberConnected(hub, sharedSubscriberId, initialWriter, CreateServerCallContext(initialCts.Token));

        var otherWriter = new TestServerStreamWriter<RoundRobinReconnectRaceEvent>();
        using var otherCts = new CancellationTokenSource();
        var otherTask = hub.OnSubscriberConnected(hub, otherSubscriberId, otherWriter, CreateServerCallContext(otherCts.Token));

        await WaitUntil(() => SubscriberExists<RoundRobinReconnectRaceEvent>(sharedSubscriberId) && SubscriberExists<RoundRobinReconnectRaceEvent>(otherSubscriberId));

        var reconnectWriter = new TestServerStreamWriter<RoundRobinReconnectRaceEvent>();
        using var reconnectCts = new CancellationTokenSource();
        var reconnectTask = hub.OnSubscriberConnected(hub, sharedSubscriberId, reconnectWriter, CreateServerCallContext(reconnectCts.Token));

        await WaitUntil(() => GetSubscriberConnectionCount<RoundRobinReconnectRaceEvent>(sharedSubscriberId) == 2);

        initialCts.Cancel();
        await WaitForCompletion(initialTask);

        GetSubscriberConnectionCount<RoundRobinReconnectRaceEvent>(sharedSubscriberId).ShouldBe(1);
        SubscriberIsConnected<RoundRobinReconnectRaceEvent>(sharedSubscriberId).ShouldBeTrue();

        for (var i = 1; i <= 4; i++)
            EventHubBase.AddToSubscriberQueues(new RoundRobinReconnectRaceEvent { EventID = i });

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
        using var initialCts = new CancellationTokenSource();
        var initialTask = hub.OnSubscriberConnected(hub, sharedSubscriberId, initialWriter, CreateServerCallContext(initialCts.Token));

        var reconnectWriter = new TestServerStreamWriter<StaleReconnectRaceEvent>();
        using var reconnectCts = new CancellationTokenSource();
        var reconnectTask = hub.OnSubscriberConnected(hub, sharedSubscriberId, reconnectWriter, CreateServerCallContext(reconnectCts.Token));

        var otherWriter = new TestServerStreamWriter<StaleReconnectRaceEvent>();
        using var otherCts = new CancellationTokenSource();
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

    [Fact]
    public async Task event_executor_refills_a_freed_slot_without_waiting_for_the_whole_batch_when_records_have_identity()
    {
        TestEventExecutorHandler.Reset();
        var storage = new TestEventSubscriberStorage();
        var handler = new TestEventExecutorHandler(storage);
        var services = CreateServiceProvider(s => s.AddSingleton<IEventHandler<TrackedTestEvent>>(handler));
        var logger = services.GetRequiredService<ILogger<
            EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>>>();
        ObjectFactory factory = static (_, _) => throw new InvalidOperationException("Handler factory should not be used in this test.");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var subscriberId = "refill-sub";

        await storage.StoreEventAsync(new()
        {
            TrackingID = Guid.NewGuid(),
            SubscriberID = subscriberId,
            EventType = typeof(TrackedTestEvent).FullName!,
            Event = new TrackedTestEvent { Name = "slow" },
            ExpireOn = DateTime.UtcNow.AddMinutes(1)
        }, cts.Token);
        await storage.StoreEventAsync(new()
        {
            TrackingID = Guid.NewGuid(),
            SubscriberID = subscriberId,
            EventType = typeof(TrackedTestEvent).FullName!,
            Event = new TrackedTestEvent { Name = "fast" },
            ExpireOn = DateTime.UtcNow.AddMinutes(1)
        }, cts.Token);
        await storage.StoreEventAsync(new()
        {
            TrackingID = Guid.NewGuid(),
            SubscriberID = subscriberId,
            EventType = typeof(TrackedTestEvent).FullName!,
            Event = new TrackedTestEvent { Name = "third" },
            ExpireOn = DateTime.UtcNow.AddMinutes(1)
        }, cts.Token);

        var executor = EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>
                       .EventExecutorTask(
                           storage,
                           new SemaphoreSlim(0),
                           new CallOptions(cancellationToken: cts.Token),
                           maxConcurrency: 2,
                           subscriberID: subscriberId,
                           logger,
                           factory,
                           services,
                           errorReceiver: null);

        await TestEventExecutorHandler.FastStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var thirdStartedBeforeSlowFinished = await Task.WhenAny(
                                                 TestEventExecutorHandler.ThirdStarted.Task,
                                                 Task.Delay(TimeSpan.FromSeconds(1), cts.Token)) ==
                                             TestEventExecutorHandler.ThirdStarted.Task;

        thirdStartedBeforeSlowFinished.ShouldBeTrue();
        storage.MaxConcurrentExecutions.ShouldBe(2);
        storage.GetRequestedLimitsSnapshot().ShouldContain(2);
        storage.GetExecutionCount("slow").ShouldBe(1);

        TestEventExecutorHandler.ReleaseSlow();
        TestEventExecutorHandler.ReleaseThird();

        await WaitUntil(() => storage.AllCompleted("slow", "fast", "third"), timeoutMs: 5000);

        cts.Cancel();
        await WaitForCompletion(executor, timeoutMs: 5000);
        storage.GetExecutionCount("slow").ShouldBe(1);
        storage.GetExecutionCount("fast").ShouldBe(1);
        storage.GetExecutionCount("third").ShouldBe(1);
    }

    [Fact]
    public async Task event_executor_ignores_exception_receiver_failures()
    {
        TestEventExecutorHandler.Reset();
        var storage = new TestEventSubscriberStorage();
        var handler = new TestEventExecutorHandler(storage);
        var provider = CreateServiceProvider(
            s =>
            {
                s.AddSingleton<IEventHandler<TrackedTestEvent>>(handler);
                s.AddSingleton<SubscriberExceptionReceiver, ThrowingSubscriberExceptionReceiver>();
            });
        var logger = provider.GetRequiredService<ILogger<
            EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>>>();
        var receiver = provider.GetRequiredService<SubscriberExceptionReceiver>();
        ObjectFactory factory = static (_, _) => throw new InvalidOperationException("Handler factory should not be used in this test.");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var subscriberId = "receiver-sub";

        await storage.StoreEventAsync(new()
        {
            TrackingID = Guid.NewGuid(),
            SubscriberID = subscriberId,
            EventType = typeof(TrackedTestEvent).FullName!,
            Event = new TrackedTestEvent { Name = "retry" },
            ExpireOn = DateTime.UtcNow.AddMinutes(1)
        }, cts.Token);

        var executor = EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>
                       .EventExecutorTask(
                           storage,
                           new SemaphoreSlim(0),
                           new CallOptions(cancellationToken: cts.Token),
                           maxConcurrency: 1,
                           subscriberID: subscriberId,
                           logger,
                           factory,
                           provider,
                           receiver);

        await TestEventExecutorHandler.RetryObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        TestEventExecutorHandler.ReleaseRetry();
        await WaitUntil(() => storage.AllCompleted("retry"), timeoutMs: 8000);

        cts.Cancel();
        await WaitForCompletion(executor, timeoutMs: 5000);
        storage.GetExecutionCount("retry").ShouldBe(2);
    }

    [Fact]
    public async Task event_executor_marks_successful_durable_events_complete_during_shutdown()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var storage = new CancellationAwareTestEventSubscriberStorage();
        var handler = new ShutdownAfterHandleEventHandler(storage, cts);
        var provider = CreateServiceProvider(s => s.AddSingleton<IEventHandler<TrackedTestEvent>>(handler));
        var logger = provider.GetRequiredService<ILogger<
            EventSubscriber<TrackedTestEvent, ShutdownAfterHandleEventHandler, TestEventRecord, CancellationAwareTestEventSubscriberStorage>>>();
        ObjectFactory factory = static (_, _) => throw new InvalidOperationException("Handler factory should not be used in this test.");
        var subscriberId = "shutdown-sub";

        await storage.StoreEventAsync(new()
        {
            TrackingID = Guid.NewGuid(),
            SubscriberID = subscriberId,
            EventType = typeof(TrackedTestEvent).FullName!,
            Event = new TrackedTestEvent { Name = "shutdown" },
            ExpireOn = DateTime.UtcNow.AddMinutes(1)
        }, cts.Token);

        var executor = EventSubscriber<TrackedTestEvent, ShutdownAfterHandleEventHandler, TestEventRecord, CancellationAwareTestEventSubscriberStorage>
                       .EventExecutorTask(
                           storage,
                           new SemaphoreSlim(0),
                           new CallOptions(cancellationToken: cts.Token),
                           maxConcurrency: 1,
                           subscriberID: subscriberId,
                           logger,
                           factory,
                           provider,
                           errorReceiver: null);

        await storage.MarkCompleteObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForCompletion(executor, timeoutMs: 5000);

        storage.AllCompleted("shutdown").ShouldBeTrue();
        storage.MarkCompleteTokenCanBeCanceled.ShouldBeFalse();
        storage.GetExecutionCount("shutdown").ShouldBe(1);
    }

    [Fact]
    public async Task event_receiver_persists_durable_events_during_shutdown()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var storage = new CancellationAwareStoreEventSubscriberStorage(cts);
        var provider = CreateServiceProvider();
        var logger = provider.GetRequiredService<ILogger<
            EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, CancellationAwareStoreEventSubscriberStorage>>>();
        var eventMessage = new TrackedTestEvent { Name = "shutdown" };

        var receiver = EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, CancellationAwareStoreEventSubscriberStorage>
                       .EventReceiverTask(
                           storage,
                           new SemaphoreSlim(0),
                           new CallOptions(cancellationToken: cts.Token),
                           new TestCallInvoker(eventMessage),
                           new Method<string, TrackedTestEvent>(
                               MethodType.ServerStreaming,
                               typeof(TrackedTestEvent).FullName!,
                               "sub",
                               new MessagePackMarshaller<string>(),
                               new MessagePackMarshaller<TrackedTestEvent>()),
                           subscriberID: "shutdown-sub",
                           eventRecordExpiry: TimeSpan.FromMinutes(1),
                           logger,
                           errors: null);

        await storage.StoreObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForCompletion(receiver, timeoutMs: 5000);

        storage.StoredRecords.ShouldHaveSingleItem();
        storage.StoreTokenCanBeCanceled.ShouldBeFalse();
        storage.StoredRecords.Single().SubscriberID.ShouldBe("shutdown-sub");
        storage.StoredRecords.Single().IsComplete.ShouldBeFalse();
        storage.StoredRecords.Single().EventType.ShouldBe(typeof(TrackedTestEvent).FullName);
        ((TrackedTestEvent)storage.StoredRecords.Single().Event).Name.ShouldBe("shutdown");
    }

    [Fact]
    public async Task event_receiver_reconnects_when_stream_completes_gracefully()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var storage = new TestEventSubscriberStorage();
        var provider = CreateServiceProvider();
        var logger = provider.GetRequiredService<ILogger<
            EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>>>();
        var invoker = new GracefulReconnectCallInvoker(
            cts,
            expectedCalls: 3);

        var receiver = EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>
                       .EventReceiverTask(
                           storage,
                           new SemaphoreSlim(0),
                           new CallOptions(cancellationToken: cts.Token),
                           invoker,
                           new Method<string, TrackedTestEvent>(
                               MethodType.ServerStreaming,
                               typeof(TrackedTestEvent).FullName!,
                               "sub",
                               new MessagePackMarshaller<string>(),
                               new MessagePackMarshaller<TrackedTestEvent>()),
                           subscriberID: "graceful-sub",
                           eventRecordExpiry: TimeSpan.FromMinutes(1),
                           logger,
                           errors: null,
                           retryDelay: TimeSpan.FromMilliseconds(25));

        await invoker.ExpectedCallCountReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForCompletion(receiver, timeoutMs: 5000);

        invoker.CallCount.ShouldBe(3);
    }

    [Fact]
    public async Task event_hub_persists_durable_events_during_shutdown()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var observer = new EventHubStoreObserver();
        var services = new ServiceCollection();
        services.AddSingleton(cts);
        services.AddSingleton(observer);
        services.AddSingleton<IHostApplicationLifetime>(new TestHostLifetime(cts.Token));
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        var provider = services.BuildServiceProvider();
        var hub = new EventHub<TrackedTestEvent, TestEventRecord, CancellationAwareEventHubStorage>(provider);
        EventHub<TrackedTestEvent, TestEventRecord, CancellationAwareEventHubStorage>.Configure(HubMode.EventPublisher, ["known-sub-1"]);

        await hub.BroadcastEventTaskForTesting(new() { Name = "shutdown" });

        observer.StoreObserved.Task.IsCompleted.ShouldBeTrue();
        observer.StoredRecords.ShouldHaveSingleItem();
        observer.StoreTokenCanBeCanceled.ShouldBeFalse();
        observer.StoredRecords.Single().SubscriberID.ShouldBe("known-sub-1");
        observer.StoredRecords.Single().EventType.ShouldBe(typeof(TrackedTestEvent).FullName);
        ((TrackedTestEvent)observer.StoredRecords.Single().Event).Name.ShouldBe("shutdown");
    }

    [Fact]
    public async Task event_executor_filters_pending_records_by_event_type_when_explicit_id_is_reused()
    {
        TestEventExecutorHandler.Reset();
        var storage = new TestEventSubscriberStorage();
        var handler = new TestEventExecutorHandler(storage);
        var services = CreateServiceProvider(s => s.AddSingleton<IEventHandler<TrackedTestEvent>>(handler));
        var logger = services.GetRequiredService<ILogger<
            EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>>>();
        ObjectFactory factory = static (_, _) => throw new InvalidOperationException("Handler factory should not be used in this test.");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        const string subscriberId = "shared-sub";

        await storage.StoreEventAsync(new()
        {
            TrackingID = Guid.NewGuid(),
            SubscriberID = subscriberId,
            EventType = typeof(TestEvent).FullName!,
            Event = new TestEvent { EventID = 999 },
            ExpireOn = DateTime.UtcNow.AddMinutes(1)
        }, cts.Token);
        await storage.StoreEventAsync(new()
        {
            TrackingID = Guid.NewGuid(),
            SubscriberID = subscriberId,
            EventType = typeof(TrackedTestEvent).FullName!,
            Event = new TrackedTestEvent { Name = "fast" },
            ExpireOn = DateTime.UtcNow.AddMinutes(1)
        }, cts.Token);

        var executor = EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>
                       .EventExecutorTask(
                           storage,
                           new SemaphoreSlim(0),
                           new CallOptions(cancellationToken: cts.Token),
                           maxConcurrency: 1,
                           subscriberID: subscriberId,
                           logger,
                           factory,
                           services,
                           errorReceiver: null);

        await TestEventExecutorHandler.FastStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitUntil(() => storage.AllCompleted("fast"), timeoutMs: 5000);

        cts.Cancel();
        await WaitForCompletion(executor, timeoutMs: 5000);
        storage.GetExecutionCount("fast").ShouldBe(1);
    }

    [Fact]
    public async Task event_hub_requeues_all_remaining_batch_records_on_stream_failure()
    {
        var provider = CreateServiceProvider();
        var hub = new EventHub<StreamFailureEvent, TestEventRecord, BatchDequeueEventHubStorage>(provider);

        // Force the in-memory provider flag so the re-queue path is exercised.
        typeof(EventHubBase)
            .GetProperty("IsInMemoryProvider", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(hub, true);

        // Retrieve the static storage the constructor created.
        var storage = (BatchDequeueEventHubStorage)typeof(EventHub<StreamFailureEvent, TestEventRecord, BatchDequeueEventHubStorage>)
            .GetField("_storage", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        var subscriberId = "stream-failure-sub";

        // Pre-load 5 events directly into the storage queue.
        for (var i = 1; i <= 5; i++)
        {
            storage.Enqueue(new TestEventRecord
            {
                TrackingID = Guid.NewGuid(),
                SubscriberID = subscriberId,
                EventType = typeof(StreamFailureEvent).FullName!,
                Event = new StreamFailureEvent { EventID = i },
                ExpireOn = DateTime.UtcNow.AddMinutes(5)
            });
        }

        // First connection: stream writer that throws after 2 successful writes.
        var failingWriter = new FailingServerStreamWriter<StreamFailureEvent>(failAfter: 2);
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var task1 = hub.OnSubscriberConnected(hub, subscriberId, failingWriter, CreateServerCallContext(cts1.Token));

        // The method should return quickly because the stream breaks on the 3rd write.
        await WaitForCompletion(task1, timeoutMs: 5000);

        // 2 events were successfully written before the stream broke.
        failingWriter.Responses.Count.ShouldBe(2);
        failingWriter.Responses[0].EventID.ShouldBe(1);
        failingWriter.Responses[1].EventID.ShouldBe(2);

        // The remaining 3 events (failed write + unattempted) must still be in the queue.
        storage.QueueCount.ShouldBe(3);

        // Reconnect with a normal writer and confirm all remaining events arrive.
        var goodWriter = new TestServerStreamWriter<StreamFailureEvent>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var task2 = hub.OnSubscriberConnected(hub, subscriberId, goodWriter, CreateServerCallContext(cts2.Token));

        await WaitUntil(() => goodWriter.Responses.Count == 3, timeoutMs: 5000);

        goodWriter.Responses.Select(e => e.EventID).ShouldBe([3, 4, 5]);

        cts2.Cancel();
        await WaitForCompletion(task2, timeoutMs: 5000);
    }

    [Fact]
    public async Task event_executor_fetches_only_available_slots_for_in_memory_provider()
    {
        InMemFetchLimitHandler.Reset();
        var storage = new BatchDequeueEventSubscriberStorage();
        var handler = new InMemFetchLimitHandler();
        var services = CreateServiceProvider(s => s.AddSingleton<IEventHandler<InMemFetchLimitEvent>>(handler));
        var logger = services.GetRequiredService<ILogger<
            EventSubscriber<InMemFetchLimitEvent, InMemFetchLimitHandler, TestEventRecord, BatchDequeueEventSubscriberStorage>>>();
        ObjectFactory factory = static (_, _) => throw new InvalidOperationException("Not used.");

        // Force the in-memory provider flag so the fetch-limit logic is exercised.
        typeof(EventSubscriber<InMemFetchLimitEvent, InMemFetchLimitHandler, TestEventRecord, BatchDequeueEventSubscriberStorage>)
            .GetField("_isInMemProvider", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, true);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var subscriberId = "fetch-limit-sub";

        // Store 5 events: the first blocks until released, the rest complete immediately.
        await storage.StoreEventAsync(new()
        {
            TrackingID = Guid.NewGuid(),
            SubscriberID = subscriberId,
            EventType = typeof(InMemFetchLimitEvent).FullName!,
            Event = new InMemFetchLimitEvent { Name = "slow" },
            ExpireOn = DateTime.UtcNow.AddMinutes(1)
        }, cts.Token);

        for (var i = 1; i <= 4; i++)
        {
            await storage.StoreEventAsync(new()
            {
                TrackingID = Guid.NewGuid(),
                SubscriberID = subscriberId,
                EventType = typeof(InMemFetchLimitEvent).FullName!,
                Event = new InMemFetchLimitEvent { Name = $"fast-{i}" },
                ExpireOn = DateTime.UtcNow.AddMinutes(1)
            }, cts.Token);
        }

        var executor = EventSubscriber<InMemFetchLimitEvent, InMemFetchLimitHandler, TestEventRecord, BatchDequeueEventSubscriberStorage>
                       .EventExecutorTask(
                           storage,
                           new SemaphoreSlim(0),
                           new CallOptions(cancellationToken: cts.Token),
                           maxConcurrency: 2,
                           subscriberID: subscriberId,
                           logger,
                           factory,
                           services,
                           errorReceiver: null);

        // Wait for all fast events to be processed while the slow event blocks one slot.
        await WaitUntil(() => InMemFetchLimitHandler.ProcessedCount >= 4, timeoutMs: 5000);

        // Release the slow event so it can complete.
        InMemFetchLimitHandler.ReleaseSlow();

        // All 5 events must be processed — none lost.
        await WaitUntil(() => InMemFetchLimitHandler.ProcessedCount == 5, timeoutMs: 5000);
        InMemFetchLimitHandler.ProcessedCount.ShouldBe(5);

        // With the fix, at least one fetch requested only the available slots (1) instead of maxConcurrency (2).
        storage.GetRequestedLimitsSnapshot().ShouldContain(1);

        cts.Cancel();
        await WaitForCompletion(executor, timeoutMs: 5000);
    }

    static ServiceProvider CreateServiceProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(A.Fake<IHostApplicationLifetime>());
        configure?.Invoke(services);

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

    static int GetSubscriberConnectionCount<TEvent>(string subscriberId) where TEvent : class, IEvent
    {
        TryGetSubscriber(typeof(TEvent), subscriberId, out var subscriber).ShouldBeTrue();

        return (int)subscriber!.GetType().GetProperty("ConnectionCount", BindingFlags.Instance | BindingFlags.Public)!.GetValue(subscriber)!;
    }

    static bool SubscriberIsConnected<TEvent>(string subscriberId) where TEvent : class, IEvent
    {
        TryGetSubscriber(typeof(TEvent), subscriberId, out var subscriber).ShouldBeTrue();

        return (bool)subscriber!.GetType().GetProperty("IsConnected", BindingFlags.Instance | BindingFlags.Public)!.GetValue(subscriber)!;
    }

    static void SetSubscriberLastSeen<TEvent>(string subscriberId, DateTime lastSeenUtc, int connectionCount = 0) where TEvent : class, IEvent
    {
        TryGetSubscriber(typeof(TEvent), subscriberId, out var subscriber).ShouldBeTrue();
        subscriber!.GetType().GetProperty("LastSeenUtc", BindingFlags.Instance | BindingFlags.Public)!.SetValue(subscriber, lastSeenUtc);
        subscriber.GetType().GetProperty("ConnectionCount", BindingFlags.Instance | BindingFlags.Public)!.SetValue(subscriber, connectionCount);
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

    class RoundRobinReconnectRaceEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class StaleReconnectRaceEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class TrackedTestEvent : IEvent
    {
        public string Name { get; set; } = default!;
    }

    class StreamFailureEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class InMemFetchLimitEvent : IEvent
    {
        public string Name { get; set; } = default!;
    }

    class WaitRecoveryEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class PollDrainEvent : IEvent
    {
        public int EventID { get; set; }
    }

    sealed class TestEventRecord : IEventStorageRecord
    {
        public string SubscriberID { get; set; } = default!;
        public Guid TrackingID { get; set; }
        public object Event { get; set; } = default!;
        public string EventType { get; set; } = default!;
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
    }

    static readonly Dictionary<string, int> _trackedEventOrder = new(StringComparer.Ordinal)
    {
        ["slow"] = 0,
        ["fast"] = 1,
        ["third"] = 2,
        ["retry"] = 3,
        ["shutdown"] = 4,
    };

    static string GetTrackedEventKey(TestEventRecord record)
        => ((TrackedTestEvent)record.Event).Name;

    static int GetTrackedEventOrder(TestEventRecord record)
        => _trackedEventOrder.TryGetValue(GetTrackedEventKey(record), out var order) ? order : int.MaxValue;

    sealed class TestEventSubscriberStorage : TestEventSubscriberStorageBase<TestEventRecord>
    {
        public TestEventSubscriberStorage()
            : base(GetTrackedEventKey, GetTrackedEventOrder)
        { }

        public int GetExecutionCount(string eventName)
            => GetExecutionCountCore(eventName);

        public bool AllCompleted(params string[] eventNames)
            => AllCompletedCore(eventNames);
    }

    abstract class TestEventSubscriberStorageBase<TRecord> : IEventSubscriberStorageProvider<TRecord> where TRecord : class, IEventStorageRecord
    {
        readonly object _sync = new();
        readonly List<TRecord> _records = [];
        readonly Dictionary<string, int> _executionCounts = new(StringComparer.Ordinal);
        readonly List<int> _requestedLimits = [];
        readonly Func<TRecord, string> _keySelector;
        readonly Func<TRecord, int> _orderSelector;
        int _activeExecutions;

        protected TestEventSubscriberStorageBase(Func<TRecord, string> keySelector, Func<TRecord, int> orderSelector)
        {
            _keySelector = keySelector;
            _orderSelector = orderSelector;
        }

        public int MaxConcurrentExecutions { get; private set; }

        public ValueTask StoreEventAsync(TRecord r, CancellationToken ct)
        {
            lock (_sync)
                _records.Add(r);

            return default;
        }

        public ValueTask<IEnumerable<TRecord>> GetNextBatchAsync(PendingRecordSearchParams<TRecord> parameters)
        {
            lock (_sync)
            {
                _requestedLimits.Add(parameters.Limit);

                var batch = _records.Where(parameters.Match.Compile())
                                    .OrderBy(_orderSelector)
                                    .Take(parameters.Limit)
                                    .ToArray();

                return new(batch);
            }
        }

        public virtual ValueTask MarkEventAsCompleteAsync(TRecord r, CancellationToken ct)
        {
            r.IsComplete = true;

            return default;
        }

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<TRecord> parameters)
            => default;

        public IReadOnlyList<int> GetRequestedLimitsSnapshot()
        {
            lock (_sync)
                return _requestedLimits.ToArray();
        }

        public void OnExecutionStarted(string key)
        {
            lock (_sync)
            {
                _activeExecutions++;
                MaxConcurrentExecutions = Math.Max(MaxConcurrentExecutions, _activeExecutions);
                _executionCounts.TryGetValue(key, out var count);
                _executionCounts[key] = count + 1;
            }
        }

        public void OnExecutionCompleted()
        {
            lock (_sync)
                _activeExecutions--;
        }

        protected int GetExecutionCountCore(string key)
        {
            lock (_sync)
                return _executionCounts.TryGetValue(key, out var count) ? count : 0;
        }

        protected bool AllCompletedCore(params string[] keys)
        {
            lock (_sync)
            {
                return keys.All(key =>
                    _records.Any(r =>
                        r.IsComplete &&
                        string.Equals(_keySelector(r), key, StringComparison.Ordinal)));
            }
        }

    }

    sealed class CancellationAwareTestEventSubscriberStorage : TestEventSubscriberStorageBase<TestEventRecord>
    {
        public CancellationAwareTestEventSubscriberStorage()
            : base(GetTrackedEventKey, GetTrackedEventOrder)
        { }

        public TaskCompletionSource MarkCompleteObserved { get; } = NewSignal();
        public bool MarkCompleteTokenCanBeCanceled { get; private set; }

        public bool AllCompleted(params string[] eventNames)
            => AllCompletedCore(eventNames);

        public int GetExecutionCount(string eventName)
            => GetExecutionCountCore(eventName);

        public override ValueTask MarkEventAsCompleteAsync(TestEventRecord record, CancellationToken ct)
        {
            MarkCompleteTokenCanBeCanceled = ct.CanBeCanceled;

            if (ct.IsCancellationRequested)
                return ValueTask.FromException(new OperationCanceledException(ct));

            record.IsComplete = true;
            MarkCompleteObserved.TrySetResult();

            return default;
        }
    }

    sealed class CancellationAwareStoreEventSubscriberStorage(CancellationTokenSource shutdownCts) : IEventSubscriberStorageProvider<TestEventRecord>
    {
        public TaskCompletionSource StoreObserved { get; } = NewSignal();
        public bool StoreTokenCanBeCanceled { get; private set; }
        public List<TestEventRecord> StoredRecords { get; } = [];

        public ValueTask StoreEventAsync(TestEventRecord r, CancellationToken ct)
        {
            StoreTokenCanBeCanceled = ct.CanBeCanceled;

            if (ct.IsCancellationRequested)
                return ValueTask.FromException(new OperationCanceledException(ct));

            StoredRecords.Add(r);
            StoreObserved.TrySetResult();
            shutdownCts.Cancel();

            return default;
        }

        public ValueTask<IEnumerable<TestEventRecord>> GetNextBatchAsync(PendingRecordSearchParams<TestEventRecord> parameters)
            => new(Array.Empty<TestEventRecord>());

        public ValueTask MarkEventAsCompleteAsync(TestEventRecord r, CancellationToken ct)
            => default;

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<TestEventRecord> parameters)
            => default;
    }

    sealed class EventHubStoreObserver
    {
        public TaskCompletionSource StoreObserved { get; } = NewSignal();
        public bool StoreTokenCanBeCanceled { get; set; }
        public List<TestEventRecord> StoredRecords { get; } = [];
    }

    sealed class CancellationAwareEventHubStorage(CancellationTokenSource shutdownCts, EventHubStoreObserver observer) : IEventHubStorageProvider<TestEventRecord>
    {
        public ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<TestEventRecord> parameters)
            => new(Array.Empty<string>());

        public ValueTask StoreEventsAsync(IEnumerable<TestEventRecord> r, CancellationToken ct)
        {
            observer.StoreTokenCanBeCanceled = ct.CanBeCanceled;

            if (ct.IsCancellationRequested)
                return ValueTask.FromException(new OperationCanceledException(ct));

            observer.StoredRecords.AddRange(r.Select(Clone));
            observer.StoreObserved.TrySetResult();
            shutdownCts.Cancel();

            return default;
        }

        public ValueTask<IEnumerable<TestEventRecord>> GetNextBatchAsync(PendingRecordSearchParams<TestEventRecord> parameters)
            => new(Array.Empty<TestEventRecord>());

        public ValueTask MarkEventAsCompleteAsync(TestEventRecord r, CancellationToken ct)
            => default;

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<TestEventRecord> parameters)
            => default;

        static TestEventRecord Clone(TestEventRecord record)
            => new()
            {
                SubscriberID = record.SubscriberID,
                TrackingID = record.TrackingID,
                Event = record.Event,
                EventType = record.EventType,
                ExpireOn = record.ExpireOn,
                IsComplete = record.IsComplete
            };
    }

    class TestEventExecutorHandler(TestEventSubscriberStorage storage) : IEventHandler<TrackedTestEvent>
    {
        internal static TaskCompletionSource FastStarted { get; private set; } = NewSignal();
        internal static TaskCompletionSource ThirdStarted { get; private set; } = NewSignal();
        internal static TaskCompletionSource RetryObserved { get; private set; } = NewSignal();
        static TaskCompletionSource _slowCanFinish = NewSignal();
        static TaskCompletionSource _thirdCanFinish = NewSignal();
        static TaskCompletionSource _retryCanSucceed = NewSignal();
        static int _retryAttempts;

        public static void Reset()
        {
            FastStarted = NewSignal();
            ThirdStarted = NewSignal();
            RetryObserved = NewSignal();
            _slowCanFinish = NewSignal();
            _thirdCanFinish = NewSignal();
            _retryCanSucceed = NewSignal();
            _retryAttempts = 0;
        }

        public static void ReleaseSlow()
            => _slowCanFinish.TrySetResult();

        public static void ReleaseThird()
            => _thirdCanFinish.TrySetResult();

        public static void ReleaseRetry()
            => _retryCanSucceed.TrySetResult();

        public async Task HandleAsync(TrackedTestEvent evnt, CancellationToken ct)
        {
            storage.OnExecutionStarted(evnt.Name);

            try
            {
                switch (evnt.Name)
                {
                    case "slow":
                        await _slowCanFinish.Task.WaitAsync(ct);
                        break;
                    case "fast":
                        FastStarted.TrySetResult();
                        break;
                    case "third":
                        ThirdStarted.TrySetResult();
                        await _thirdCanFinish.Task.WaitAsync(ct);
                        break;
                    case "retry":
                        if (Interlocked.Increment(ref _retryAttempts) == 1)
                        {
                            RetryObserved.TrySetResult();
                            throw new InvalidOperationException("boom");
                        }

                        await _retryCanSucceed.Task.WaitAsync(ct);
                        break;
                }
            }
            finally
            {
                storage.OnExecutionCompleted();
            }
        }
    }

    sealed class ShutdownAfterHandleEventHandler(CancellationAwareTestEventSubscriberStorage storage, CancellationTokenSource shutdownCts)
        : IEventHandler<TrackedTestEvent>
    {
        public Task HandleAsync(TrackedTestEvent evnt, CancellationToken ct)
        {
            storage.OnExecutionStarted(evnt.Name);

            try
            {
                shutdownCts.Cancel();

                return Task.CompletedTask;
            }
            finally
            {
                storage.OnExecutionCompleted();
            }
        }
    }

    sealed class ThrowingSubscriberExceptionReceiver : SubscriberExceptionReceiver
    {
        public override Task OnHandlerExecutionError<TEvent, THandler>(IEventStorageRecord record,
                                                                       int attemptCount,
                                                                       Exception exception,
                                                                       CancellationToken ct)
            => throw new InvalidOperationException("receiver failure");
    }

    static TaskCompletionSource NewSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    class TestServerStreamWriter<T> : IServerStreamWriter<T>
    {
        public WriteOptions? WriteOptions { get; set; }
        public List<T> Responses { get; } = new();

        public async Task WriteAsync(T message)
            => Responses.Add(message);

        public Task WriteAsync(T message, CancellationToken ct)
            => WriteAsync(message);
    }

    sealed class GateServerStreamWriter<T> : IServerStreamWriter<T>
    {
        readonly TaskCompletionSource _gate = NewSignal();

        public WriteOptions? WriteOptions { get; set; }
        public List<T> Responses { get; } = new();

        public void Release()
            => _gate.TrySetResult();

        public async Task WriteAsync(T message)
        {
            await _gate.Task;
            Responses.Add(message);
        }

        public Task WriteAsync(T message, CancellationToken ct)
            => WriteAsync(message);
    }

    sealed class TestCallInvoker(TrackedTestEvent eventMessage) : CallInvoker
    {
        readonly AsyncServerStreamingCall<TrackedTestEvent> _call = new(
            new SingleMessageAsyncStreamReader<TrackedTestEvent>(eventMessage),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                                                      string? host,
                                                                                                                      CallOptions options)
            => throw new NotSupportedException();

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                                                      string? host,
                                                                                                                      CallOptions options)
            => throw new NotSupportedException();

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                                            string? host,
                                                                                                            CallOptions options,
                                                                                                            TRequest request)
            => (AsyncServerStreamingCall<TResponse>)(object)_call;

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                       string? host,
                                                                                       CallOptions options,
                                                                                       TRequest request)
            => throw new NotSupportedException();

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                          string? host,
                                                                          CallOptions options,
                                                                          TRequest request)
            => throw new NotSupportedException();
    }

    sealed class GracefulReconnectCallInvoker(CancellationTokenSource shutdownCts, int expectedCalls) : CallInvoker
    {
        int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);
        public TaskCompletionSource ExpectedCallCountReached { get; } = NewSignal();

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                                                      string? host,
                                                                                                                      CallOptions options)
            => throw new NotSupportedException();

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                                                      string? host,
                                                                                                                      CallOptions options)
            => throw new NotSupportedException();

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                                            string? host,
                                                                                                            CallOptions options,
                                                                                                            TRequest request)
        {
            if (Interlocked.Increment(ref _callCount) == expectedCalls)
            {
                ExpectedCallCountReached.TrySetResult();
                shutdownCts.Cancel();
            }

            var call = new AsyncServerStreamingCall<TrackedTestEvent>(
                new EmptyAsyncStreamReader<TrackedTestEvent>(),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });

            return (AsyncServerStreamingCall<TResponse>)(object)call;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                       string? host,
                                                                                       CallOptions options,
                                                                                       TRequest request)
            => throw new NotSupportedException();

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                          string? host,
                                                                          CallOptions options,
                                                                          TRequest request)
            => throw new NotSupportedException();
    }

    sealed class SingleMessageAsyncStreamReader<T>(T message) : IAsyncStreamReader<T>
    {
        bool _moved;

        public T Current { get; private set; } = default!;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (_moved)
                return Task.FromResult(false);

            _moved = true;
            Current = message;

            return Task.FromResult(true);
        }
    }

    sealed class EmptyAsyncStreamReader<T> : IAsyncStreamReader<T>
    {
        public T Current => default!;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
            => Task.FromResult(false);
    }

    sealed class TestHostLifetime(CancellationToken appStoppingToken) : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => appStoppingToken;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() { }
    }

    class FailingServerStreamWriter<T>(int failAfter) : IServerStreamWriter<T>
    {
        int _writeCount;

        public WriteOptions? WriteOptions { get; set; }
        public List<T> Responses { get; } = new();

        public Task WriteAsync(T message)
        {
            if (_writeCount >= failAfter)
                throw new InvalidOperationException("Simulated stream failure");

            _writeCount++;
            Responses.Add(message);

            return Task.CompletedTask;
        }

        public Task WriteAsync(T message, CancellationToken ct)
            => WriteAsync(message);
    }

    sealed class BatchDequeueEventHubStorage : IEventHubStorageProvider<TestEventRecord>
    {
        readonly object _sync = new();
        readonly Queue<TestEventRecord> _queue = new();

        public void Enqueue(TestEventRecord record)
        {
            lock (_sync)
                _queue.Enqueue(record);
        }

        public int QueueCount
        {
            get
            {
                lock (_sync)
                    return _queue.Count;
            }
        }

        public ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<TestEventRecord> p)
            => new(Array.Empty<string>());

        public ValueTask StoreEventsAsync(IEnumerable<TestEventRecord> r, CancellationToken ct)
        {
            lock (_sync)
            {
                foreach (var record in r)
                    _queue.Enqueue(record);
            }

            return default;
        }

        public ValueTask<IEnumerable<TestEventRecord>> GetNextBatchAsync(PendingRecordSearchParams<TestEventRecord> p)
        {
            lock (_sync)
            {
                var batch = new List<TestEventRecord>();

                while (batch.Count < p.Limit && _queue.Count > 0)
                    batch.Add(_queue.Dequeue());

                return new(batch);
            }
        }

        public ValueTask MarkEventAsCompleteAsync(TestEventRecord r, CancellationToken ct)
            => default;

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<TestEventRecord> p)
            => default;
    }

    sealed class InstrumentedEventHubStorageState
    {
        readonly object _sync = new();
        readonly List<TestEventRecord> _records = [];

        public TaskCompletionSource SecondFetchObserved { get; } = NewSignal();
        public int EmptyBatchCount { get; private set; }
        public int GetNextBatchCallCount { get; private set; }

        public void Store(IEnumerable<TestEventRecord> records)
        {
            lock (_sync)
                _records.AddRange(records);
        }

        public IReadOnlyList<TestEventRecord> GetNextBatch(PendingRecordSearchParams<TestEventRecord> parameters)
        {
            lock (_sync)
            {
                GetNextBatchCallCount++;

                if (GetNextBatchCallCount >= 2)
                    SecondFetchObserved.TrySetResult();

                var batch = _records.Where(parameters.Match.Compile())
                                    .Take(parameters.Limit)
                                    .ToArray();

                if (batch.Length == 0)
                    EmptyBatchCount++;

                return batch;
            }
        }

        public void MarkComplete(TestEventRecord record)
        {
            lock (_sync)
                record.IsComplete = true;
        }
    }

    sealed class InstrumentedEventHubStorage(InstrumentedEventHubStorageState state) : IEventHubStorageProvider<TestEventRecord>
    {
        public ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<TestEventRecord> parameters)
            => new(Array.Empty<string>());

        public ValueTask StoreEventsAsync(IEnumerable<TestEventRecord> records, CancellationToken ct)
        {
            state.Store(records);

            return default;
        }

        public ValueTask<IEnumerable<TestEventRecord>> GetNextBatchAsync(PendingRecordSearchParams<TestEventRecord> parameters)
            => new(state.GetNextBatch(parameters));

        public ValueTask MarkEventAsCompleteAsync(TestEventRecord record, CancellationToken ct)
        {
            state.MarkComplete(record);

            return default;
        }

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<TestEventRecord> parameters)
            => default;
    }

    sealed class BatchDequeueEventSubscriberStorage : IEventSubscriberStorageProvider<TestEventRecord>
    {
        readonly object _sync = new();
        readonly Queue<TestEventRecord> _queue = new();
        readonly List<int> _requestedLimits = [];

        public ValueTask StoreEventAsync(TestEventRecord r, CancellationToken ct)
        {
            lock (_sync)
                _queue.Enqueue(r);

            return default;
        }

        public ValueTask<IEnumerable<TestEventRecord>> GetNextBatchAsync(PendingRecordSearchParams<TestEventRecord> p)
        {
            lock (_sync)
            {
                _requestedLimits.Add(p.Limit);

                var batch = new List<TestEventRecord>();

                while (batch.Count < p.Limit && _queue.Count > 0)
                    batch.Add(_queue.Dequeue());

                return new(batch);
            }
        }

        public ValueTask MarkEventAsCompleteAsync(TestEventRecord r, CancellationToken ct)
            => default;

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<TestEventRecord> p)
            => default;

        public IReadOnlyList<int> GetRequestedLimitsSnapshot()
        {
            lock (_sync)
                return [.. _requestedLimits];
        }
    }

    class InMemFetchLimitHandler : IEventHandler<InMemFetchLimitEvent>
    {
        static int _processedCount;
        static TaskCompletionSource _slowGate = NewSignal();

        public static void Reset()
        {
            Interlocked.Exchange(ref _processedCount, 0);
            _slowGate = NewSignal();
        }

        public static void ReleaseSlow()
            => _slowGate.TrySetResult();

        public static int ProcessedCount => Volatile.Read(ref _processedCount);

        public async Task HandleAsync(InMemFetchLimitEvent evnt, CancellationToken ct)
        {
            if (evnt.Name == "slow")
                await _slowGate.Task.WaitAsync(ct);

            Interlocked.Increment(ref _processedCount);
        }
    }
}
