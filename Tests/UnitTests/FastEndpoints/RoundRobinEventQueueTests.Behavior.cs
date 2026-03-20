using System.Collections.Concurrent;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using QueueTesting;
using Xunit;
using static QueueTesting.QueueTestSupport;

namespace EventQueue;

public partial class RoundRobinEventQueueTests
{
    [Fact]
    public async Task round_robin_mode_distributes_three_events_across_two_subscribers_in_order()
    {
        var provider = CreateServiceProvider();
        EventHub<RrTestEventMulti, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.RoundRobin | HubMode.EventBroker;

        var hub = new EventHub<RrTestEventMulti, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        var writerA = new TestServerStreamWriter<RrTestEventMulti>();
        var writerB = new TestServerStreamWriter<RrTestEventMulti>();

        _ = ConnectAnonymousSubscriber(hub, writerA, CancellationToken.None);
        _ = ConnectAnonymousSubscriber(hub, writerB, CancellationToken.None);

        PublishEvents<RrTestEventMulti>(111, 222, 333);

        await WaitForTotalResponses(3, writerA, writerB);

        var idsA = GetEventIds(writerA);
        var idsB = GetEventIds(writerB);

        if (idsA.Length == 2)
        {
            idsA.ShouldBe([111, 333]);
            idsB.ShouldBe([222]);

            return;
        }

        if (idsB.Length == 2)
        {
            idsA.ShouldBe([222]);
            idsB.ShouldBe([111, 333]);

            return;
        }

        throw new InvalidOperationException("Expected one subscriber to receive two events and the other to receive one.");
    }

    [Fact]
    public async Task round_robin_mode_distributes_evenly_across_three_subscribers()
    {
        var provider = CreateServiceProvider();
        EventHub<RrTestEventThreeSubs, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.RoundRobin | HubMode.EventBroker;

        var hub = new EventHub<RrTestEventThreeSubs, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        var writerA = new TestServerStreamWriter<RrTestEventThreeSubs>();
        var writerB = new TestServerStreamWriter<RrTestEventThreeSubs>();
        var writerC = new TestServerStreamWriter<RrTestEventThreeSubs>();

        _ = ConnectAnonymousSubscriber(hub, writerA, CancellationToken.None);
        _ = ConnectAnonymousSubscriber(hub, writerB, CancellationToken.None);
        _ = ConnectAnonymousSubscriber(hub, writerC, CancellationToken.None);

        PublishEvents<RrTestEventThreeSubs>(100, 200, 300, 400, 500, 600);

        await WaitForTotalResponses(6, writerA, writerB, writerC);

        writerA.Responses.Count.ShouldBe(2);
        writerB.Responses.Count.ShouldBe(2);
        writerC.Responses.Count.ShouldBe(2);

        var allIds = writerA.Responses.Select(response => response.EventID)
                            .Concat(writerB.Responses.Select(response => response.EventID))
                            .Concat(writerC.Responses.Select(response => response.EventID))
                            .OrderBy(id => id)
                            .ToArray();

        allIds.ShouldBe([100, 200, 300, 400, 500, 600]);
    }

    [Fact]
    public async Task round_robin_mode_skips_an_offline_subscriber_and_delivers_to_the_remaining_connection()
    {
        var provider = CreateServiceProvider();
        EventHub<RrTestEventOneConnected, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.RoundRobin;

        var hub = new EventHub<RrTestEventOneConnected, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        var writerA = new TestServerStreamWriter<RrTestEventOneConnected>();
        var writerB = new TestServerStreamWriter<RrTestEventOneConnected>();

        _ = ConnectAnonymousSubscriber(hub, writerA, CancellationToken.None);

        using var canceledSubscriberCts = new CancellationTokenSource(100);
        _ = ConnectAnonymousSubscriber(hub, writerB, canceledSubscriberCts.Token);

        await Task.Delay(200);

        PublishEvents<RrTestEventOneConnected>(111, 222);

        await WaitForTotalResponses(2, writerA, writerB);

        AssertExactlyOneWriterReceived([111, 222], writerA, writerB);
    }

    [Fact]
    public async Task round_robin_mode_delivers_all_events_to_the_only_connected_subscriber()
    {
        var provider = CreateServiceProvider();
        EventHub<RrTestEventOnlyOne, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.RoundRobin;

        var hub = new EventHub<RrTestEventOnlyOne, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        var writer = new TestServerStreamWriter<RrTestEventOnlyOne>();

        _ = ConnectAnonymousSubscriber(hub, writer, CancellationToken.None);

        PublishEvents<RrTestEventOnlyOne>(111, 222, 333);

        await WaitForTotalResponses(3, writer);

        GetEventIds(writer).ShouldBe([111, 222, 333]);
    }

    [Fact]
    public void configuring_known_subscribers_for_a_round_robin_hub_throws()
    {
        var configure = () => EventHub<RrKnownSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Configure(HubMode.RoundRobin, ["rr-known-sub"]);

        configure.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public async Task concurrent_round_robin_selection_advances_atomically()
    {
        var provider = CreateServiceProvider();
        EventHub<RrConcurrentSelectionEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.RoundRobin;

        var hub = new EventHub<RrConcurrentSelectionEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
        var subscriberIds = new[] { "sub-a", "sub-b" };
        var results = new ConcurrentBag<string>();
        using var start = new ManualResetEventSlim();
        var workerCount = Math.Max(Environment.ProcessorCount, 2);
        const int iterationsPerWorker = 1000;

        var tasks = Enumerable.Range(0, workerCount)
                              .Select(
                                  _ => Task.Run(
                                      () =>
                                      {
                                          start.Wait();

                                          for (var iteration = 0; iteration < iterationsPerWorker; iteration++)
                                          {
                                              results.Add(hub.GetNextRoundRobinSubscriberId(subscriberIds));
                                              Thread.Yield();
                                          }
                                      }))
                              .ToArray();

        start.Set();
        await Task.WhenAll(tasks);

        var expectedCountPerSubscriber = workerCount * iterationsPerWorker / subscriberIds.Length;

        results.Count(id => id == "sub-a").ShouldBe(expectedCountPerSubscriber);
        results.Count(id => id == "sub-b").ShouldBe(expectedCountPerSubscriber);
    }

    [Fact]
    public async Task concurrent_publishes_remain_evenly_distributed_in_round_robin_mode()
    {
        const int eventCount = 200;
        var state = new RoundRobinRecordingStorageState();
        var provider = CreateServiceProvider(services => services.AddSingleton(state));

        EventHub<RrConcurrentPublishEvent, RoundRobinRecordingRecord, RoundRobinRecordingStorage>.Mode = HubMode.RoundRobin;

        var hub = new EventHub<RrConcurrentPublishEvent, RoundRobinRecordingRecord, RoundRobinRecordingStorage>(provider);
        using var ctsA = new CancellationTokenSource();
        using var ctsB = new CancellationTokenSource();
        var subscriberTaskA = ConnectSubscriber(hub, "sub-a", new(), ctsA.Token);
        var subscriberTaskB = ConnectSubscriber(hub, "sub-b", new(), ctsB.Token);

        await WaitUntil(
            () => SubscriberExists<RrConcurrentPublishEvent, RoundRobinRecordingRecord, RoundRobinRecordingStorage>("sub-a") &&
                  SubscriberExists<RrConcurrentPublishEvent, RoundRobinRecordingRecord, RoundRobinRecordingStorage>("sub-b"));

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publishTasks = Enumerable.Range(1, eventCount)
                                     .Select(
                                         eventId => Task.Run(
                                             async () =>
                                             {
                                                 await start.Task;
                                                 await hub.BroadcastEventTaskForTesting(new() { EventID = eventId });
                                             }))
                                     .ToArray();

        start.TrySetResult();
        await Task.WhenAll(publishTasks);

        var stored = state.GetStoredRecords();

        stored.Count.ShouldBe(eventCount);
        stored.Select(record => ((RrConcurrentPublishEvent)record.Event).EventID).Distinct().Count().ShouldBe(eventCount);
        stored.Count(record => record.SubscriberID == "sub-a").ShouldBe(eventCount / 2);
        stored.Count(record => record.SubscriberID == "sub-b").ShouldBe(eventCount / 2);

        ctsA.Cancel();
        ctsB.Cancel();

        await WaitForCompletion(subscriberTaskA, timeoutMs: 5000);
        await WaitForCompletion(subscriberTaskB, timeoutMs: 5000);
    }
}
