using System.Diagnostics;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using QueueTesting;
using Xunit;
using static QueueTesting.QueueTestSupport;

namespace EventQueue;

public partial class EventQueueTests
{
    [Fact]
    public async Task event_hub_publisher_mode_broadcasts_events_to_connected_subscribers()
    {
        var provider = CreateServiceProvider();
        EventHub<TestEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.EventPublisher;

        var hub = new EventHub<TestEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        var writer = new TestServerStreamWriter<TestEvent>();

        _ = hub.OnSubscriberConnected(hub, Guid.NewGuid().ToString(), writer, CreateServerCallContext(CancellationToken.None));
        _ = hub.OnSubscriberConnected(hub, Guid.NewGuid().ToString(), writer, CreateServerCallContext(CancellationToken.None));

        EventHubBase.AddToSubscriberQueues(new TestEvent { EventID = 123 });

        await WaitUntil(() => writer.Responses.Count >= 1);
        writer.Responses[0].EventID.ShouldBe(123);
    }

    [Fact]
    public async Task event_hub_broker_mode_forwards_received_events_to_subscribers()
    {
        var provider = CreateServiceProvider();
        EventHub<TestEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.EventBroker;

        var hub = new EventHub<TestEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        var writer = new TestServerStreamWriter<TestEvent>();
        var context = CreateServerCallContext(CancellationToken.None);

        _ = hub.OnSubscriberConnected(hub, Guid.NewGuid().ToString(), writer, context);
        _ = hub.OnEventReceived(hub, new TestEvent { EventID = 321 }, context);

        await WaitUntil(() => writer.Responses.Count == 1);
        writer.Responses.Single().EventID.ShouldBe(321);
    }

    [Fact]
    public async Task event_hub_wakes_immediately_after_poll_timeout_when_new_event_arrives()
    {
        var state = new InstrumentedEventHubStorageState();
        var provider = CreateServiceProvider(services => services.AddSingleton(state));

        EventHub<WaitRecoveryEvent, TestEventRecord, InstrumentedEventHubStorage>.Mode = HubMode.EventPublisher;
        EventHub<WaitRecoveryEvent, TestEventRecord, InstrumentedEventHubStorage>.WaitForSignalTimeout = TimeSpan.FromMilliseconds(200);

        var hub = new EventHub<WaitRecoveryEvent, TestEventRecord, InstrumentedEventHubStorage>(provider);
        var writer = new TestServerStreamWriter<WaitRecoveryEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        Task? subscriberTask = null;

        try
        {
            subscriberTask = hub.OnSubscriberConnected(hub, "wait-recovery-sub", writer, CreateServerCallContext(cts.Token));

            await state.SecondFetchObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Task.Delay(50);

            var stopwatch = Stopwatch.StartNew();
            await hub.BroadcastEventTaskForTesting(new WaitRecoveryEvent { EventID = 42 });
            await WaitUntil(() => writer.Responses.Count == 1, timeoutMs: 2000);

            stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(500));
            writer.Responses.Single().EventID.ShouldBe(42);
        }
        finally
        {
            cts.Cancel();

            if (subscriberTask is not null)
                await WaitForCompletion(subscriberTask, timeoutMs: 5000);

            EventHub<WaitRecoveryEvent, TestEventRecord, InstrumentedEventHubStorage>.WaitForSignalTimeout = TimeSpan.FromSeconds(10);
        }
    }

    [Fact]
    public async Task event_hub_drains_residual_semaphore_releases_after_processing_backlog()
    {
        const int eventCount = 100;
        var state = new InstrumentedEventHubStorageState();
        var provider = CreateServiceProvider(services => services.AddSingleton(state));

        EventHub<PollDrainEvent, TestEventRecord, InstrumentedEventHubStorage>.Mode = HubMode.EventPublisher;

        var hub = new EventHub<PollDrainEvent, TestEventRecord, InstrumentedEventHubStorage>(provider);
        var writer = new GateServerStreamWriter<PollDrainEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var subscriberTask = hub.OnSubscriberConnected(hub, "poll-drain-sub", writer, CreateServerCallContext(cts.Token));

        for (var eventId = 1; eventId <= eventCount; eventId++)
            await hub.BroadcastEventTaskForTesting(new PollDrainEvent { EventID = eventId });

        writer.Release();

        await WaitUntil(() => writer.Responses.Count == eventCount, timeoutMs: 5000);
        await Task.Delay(250);

        state.GetNextBatchCallCount.ShouldBeLessThan(12);

        cts.Cancel();
        await WaitForCompletion(subscriberTask, timeoutMs: 5000);
    }

    [Fact]
    public async Task event_hub_requeues_all_remaining_batch_records_on_stream_failure()
    {
        var provider = CreateServiceProvider();
        var hub = new EventHub<StreamFailureEvent, TestEventRecord, BatchDequeueEventHubStorage>(provider);
        var storage = GetStaticHubStorage<StreamFailureEvent, TestEventRecord, BatchDequeueEventHubStorage>();
        const string subscriberId = "stream-failure-sub";

        SetIsInMemoryProvider(hub);

        for (var eventId = 1; eventId <= 5; eventId++)
            storage.Enqueue(CreateTestRecord(subscriberId, new StreamFailureEvent { EventID = eventId }, DateTime.UtcNow.AddMinutes(5)));

        var failingWriter = new FailingServerStreamWriter<StreamFailureEvent>(failAfter: 2);
        using var firstConnectionCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var firstConnection = hub.OnSubscriberConnected(hub, subscriberId, failingWriter, CreateServerCallContext(firstConnectionCts.Token));

        await WaitForCompletion(firstConnection, timeoutMs: 5000);

        failingWriter.Responses.Count.ShouldBe(2);
        failingWriter.Responses[0].EventID.ShouldBe(1);
        failingWriter.Responses[1].EventID.ShouldBe(2);
        storage.QueueCount.ShouldBe(3);

        var recoveryWriter = new TestServerStreamWriter<StreamFailureEvent>();
        using var secondConnectionCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var secondConnection = hub.OnSubscriberConnected(hub, subscriberId, recoveryWriter, CreateServerCallContext(secondConnectionCts.Token));

        await WaitUntil(() => recoveryWriter.Responses.Count == 3, timeoutMs: 5000);

        recoveryWriter.Responses.Select(response => response.EventID).ShouldBe([3, 4, 5]);

        secondConnectionCts.Cancel();
        await WaitForCompletion(secondConnection, timeoutMs: 5000);
    }
}
