using System.Collections.Concurrent;
using System.Reflection;
using FakeItEasy;
using FastEndpoints;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EventQueue;

public class RoundRobinEventQueueTests
{
    [Fact]
    public async Task multiple_subscribers()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(A.Fake<IHostApplicationLifetime>());
        var provider = services.BuildServiceProvider();
        EventHub<RRTestEventMulti, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.RoundRobin | HubMode.EventBroker;
        var hub = new EventHub<RRTestEventMulti, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);

        var writerA = new TestServerStreamWriter<RRTestEventMulti>();
        var writerB = new TestServerStreamWriter<RRTestEventMulti>();

        var ctx = A.Fake<ServerCallContext>();
        A.CallTo(ctx).WithReturnType<CancellationToken>().Returns(default);

        _ = hub.OnSubscriberConnected(hub, Guid.NewGuid().ToString(), writerA, ctx);
        _ = hub.OnSubscriberConnected(hub, Guid.NewGuid().ToString(), writerB, ctx);

        var e1 = new RRTestEventMulti { EventID = 111 };
        EventHubBase.AddToSubscriberQueues(e1);

        var e2 = new RRTestEventMulti { EventID = 222 };
        EventHubBase.AddToSubscriberQueues(e2);

        var e3 = new RRTestEventMulti { EventID = 333 };
        EventHubBase.AddToSubscriberQueues(e3);

        while (writerA.Responses.Count + writerB.Responses.Count < 3)
            await Task.Delay(100);

        if (writerA.Responses.Count == 2)
        {
            writerB.Responses.Count.ShouldBe(1);
            writerB.Responses[0].EventID.ShouldBe(222);

            writerA.Responses[0].EventID.ShouldBe(111);
            writerA.Responses[1].EventID.ShouldBe(333);
        }
        else if (writerB.Responses.Count == 2)
        {
            writerA.Responses.Count.ShouldBe(1);
            writerA.Responses[0].EventID.ShouldBe(222);

            writerB.Responses[0].EventID.ShouldBe(111);
            writerB.Responses[1].EventID.ShouldBe(333);
        }
        else
            throw new();
    }

    [Fact]
    public async Task three_subscribers_all_receive_events()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(A.Fake<IHostApplicationLifetime>());
        var provider = services.BuildServiceProvider();
        EventHub<RRTestEventThreeSubs, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.RoundRobin | HubMode.EventBroker;
        var hub = new EventHub<RRTestEventThreeSubs, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);

        var writerA = new TestServerStreamWriter<RRTestEventThreeSubs>();
        var writerB = new TestServerStreamWriter<RRTestEventThreeSubs>();
        var writerC = new TestServerStreamWriter<RRTestEventThreeSubs>();

        var ctx = A.Fake<ServerCallContext>();
        A.CallTo(ctx).WithReturnType<CancellationToken>().Returns(default);

        _ = hub.OnSubscriberConnected(hub, Guid.NewGuid().ToString(), writerA, ctx);
        _ = hub.OnSubscriberConnected(hub, Guid.NewGuid().ToString(), writerB, ctx);
        _ = hub.OnSubscriberConnected(hub, Guid.NewGuid().ToString(), writerC, ctx);

        for (var i = 1; i <= 6; i++)
            EventHubBase.AddToSubscriberQueues(new RRTestEventThreeSubs { EventID = i * 100 });

        while (writerA.Responses.Count + writerB.Responses.Count + writerC.Responses.Count < 6)
            await Task.Delay(100);

        // each subscriber should receive exactly 2 of the 6 events
        writerA.Responses.Count.ShouldBe(2);
        writerB.Responses.Count.ShouldBe(2);
        writerC.Responses.Count.ShouldBe(2);

        // all 6 event IDs should be present across all subscribers (no duplicates, no losses)
        var allIds = writerA.Responses.Select(e => e.EventID)
                            .Concat(writerB.Responses.Select(e => e.EventID))
                            .Concat(writerC.Responses.Select(e => e.EventID))
                            .OrderBy(id => id)
                            .ToArray();

        allIds.ShouldBe(new[] { 100, 200, 300, 400, 500, 600 });
    }

    [Fact]
    public async Task multiple_subscribers_but_one_goes_offline()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(A.Fake<IHostApplicationLifetime>());
        var provider = services.BuildServiceProvider();
        EventHub<RRTestEventOneConnected, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.RoundRobin;
        var hub = new EventHub<RRTestEventOneConnected, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);

        var writerA = new TestServerStreamWriter<RRTestEventOneConnected>();
        var writerB = new TestServerStreamWriter<RRTestEventOneConnected>();

        var ctxA = A.Fake<ServerCallContext>();
        A.CallTo(ctxA).WithReturnType<CancellationToken>().Returns(default);
        _ = hub.OnSubscriberConnected(hub, Guid.NewGuid().ToString(), writerA, ctxA);

        var ctxB = A.Fake<ServerCallContext>();
        var cts = new CancellationTokenSource(100);
        A.CallTo(ctxB).WithReturnType<CancellationToken>().Returns(cts.Token);
        _ = hub.OnSubscriberConnected(hub, Guid.NewGuid().ToString(), writerB, ctxB);

        await Task.Delay(200); //subscriber B is cancelled by now

        var e1 = new RRTestEventOneConnected { EventID = 111 };
        EventHubBase.AddToSubscriberQueues(e1);

        var e2 = new RRTestEventOneConnected { EventID = 222 };
        EventHubBase.AddToSubscriberQueues(e2);

        while (writerA.Responses.Count + writerB.Responses.Count < 2)
            await Task.Delay(100);

        if (writerA.Responses.Count == 2)
        {
            writerA.Responses[0].EventID.ShouldBe(111);
            writerA.Responses[1].EventID.ShouldBe(222);
            writerB.Responses.Count.ShouldBe(0);
        }
        else if (writerB.Responses.Count == 2)
        {
            writerB.Responses[0].EventID.ShouldBe(111);
            writerB.Responses[1].EventID.ShouldBe(222);
            writerA.Responses.Count.ShouldBe(0);
        }
        else
            throw new();
    }

    [Fact]
    public async Task only_one_subscriber()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(A.Fake<IHostApplicationLifetime>());
        var provider = services.BuildServiceProvider();
        EventHub<RRTestEventOnlyOne, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.RoundRobin;
        var hub = new EventHub<RRTestEventOnlyOne, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);

        var writer = new TestServerStreamWriter<RRTestEventOnlyOne>();

        var ctx = A.Fake<ServerCallContext>();
        A.CallTo(ctx).WithReturnType<CancellationToken>().Returns(default);

        _ = hub.OnSubscriberConnected(hub, Guid.NewGuid().ToString(), writer, ctx);

        var e1 = new RRTestEventOnlyOne { EventID = 111 };
        EventHubBase.AddToSubscriberQueues(e1);

        var e2 = new RRTestEventOnlyOne { EventID = 222 };
        EventHubBase.AddToSubscriberQueues(e2);

        var e3 = new RRTestEventOnlyOne { EventID = 333 };
        EventHubBase.AddToSubscriberQueues(e3);

        while (writer.Responses.Count < 1)
            await Task.Delay(100);

        writer.Responses.Count.ShouldBe(3);
        writer.Responses[0].EventID.ShouldBe(111);
        writer.Responses[1].EventID.ShouldBe(222);
        writer.Responses[2].EventID.ShouldBe(333);
    }

    [Fact]
    public void configuring_known_subscribers_for_round_robin_hub_throws()
    {
        var act = () => EventHub<RRKnownSubscriberEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Configure(HubMode.RoundRobin, ["rr-known-sub"]);

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public async Task concurrent_round_robin_selection_advances_atomically()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(A.Fake<IHostApplicationLifetime>());
        var provider = services.BuildServiceProvider();
        EventHub<RRConcurrentSelectionEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>.Mode = HubMode.RoundRobin;
        var hub = new EventHub<RRConcurrentSelectionEvent, InMemoryEventStorageRecord, InMemoryEventHubStorage>(provider);
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

                                           for (var i = 0; i < iterationsPerWorker; i++)
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
        var state = new RoundRobinRecordingStorageState();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(A.Fake<IHostApplicationLifetime>());
        services.AddSingleton(state);
        var provider = services.BuildServiceProvider();
        EventHub<RRConcurrentPublishEvent, RoundRobinRecordingRecord, RoundRobinRecordingStorage>.Mode = HubMode.RoundRobin;
        var hub = new EventHub<RRConcurrentPublishEvent, RoundRobinRecordingRecord, RoundRobinRecordingStorage>(provider);

        using var ctsA = new CancellationTokenSource();
        using var ctsB = new CancellationTokenSource();
        var subscriberTaskA = hub.OnSubscriberConnected(hub, "sub-a", new TestServerStreamWriter<RRConcurrentPublishEvent>(), CreateServerCallContext(ctsA.Token));
        var subscriberTaskB = hub.OnSubscriberConnected(hub, "sub-b", new TestServerStreamWriter<RRConcurrentPublishEvent>(), CreateServerCallContext(ctsB.Token));

        await WaitUntil(
            () => SubscriberExists<RRConcurrentPublishEvent, RoundRobinRecordingRecord, RoundRobinRecordingStorage>("sub-a") &&
                  SubscriberExists<RRConcurrentPublishEvent, RoundRobinRecordingRecord, RoundRobinRecordingStorage>("sub-b"));

        const int eventCount = 200;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publishTasks = Enumerable.Range(1, eventCount)
                                     .Select(
                                          i => Task.Run(
                                              async () =>
                                              {
                                                  await start.Task;
                                                  await hub.BroadcastEventTaskForTesting(new() { EventID = i });
                                              }))
                                     .ToArray();

        start.TrySetResult();
        await Task.WhenAll(publishTasks);

        var stored = state.GetStoredRecords();

        stored.Count.ShouldBe(eventCount);
        stored.Select(r => ((RRConcurrentPublishEvent)r.Event).EventID).Distinct().Count().ShouldBe(eventCount);
        stored.Count(r => r.SubscriberID == "sub-a").ShouldBe(eventCount / 2);
        stored.Count(r => r.SubscriberID == "sub-b").ShouldBe(eventCount / 2);

        ctsA.Cancel();
        ctsB.Cancel();

        await WaitForCompletion(subscriberTaskA, timeoutMs: 5000);
        await WaitForCompletion(subscriberTaskB, timeoutMs: 5000);
    }

    static ServerCallContext CreateServerCallContext(CancellationToken ct)
    {
        var ctx = A.Fake<ServerCallContext>();
        A.CallTo(ctx).WithReturnType<CancellationToken>().Returns(ct);

        return ctx;
    }

    static bool SubscriberExists<TEvent>(string subscriberId) where TEvent : class, IEvent
        => TryGetInMemorySubscriber(typeof(TEvent), subscriberId, out _);

    static bool SubscriberExists<TEvent, TStorageRecord, TStorageProvider>(string subscriberId)
        where TEvent : class, IEvent
        where TStorageRecord : class, IEventStorageRecord, new()
        where TStorageProvider : IEventHubStorageProvider<TStorageRecord>
        => TryGetSubscriberByHubType(typeof(EventHub<TEvent, TStorageRecord, TStorageProvider>), subscriberId, out _);

    static bool TryGetInMemorySubscriber(Type eventType, string subscriberId, out object? subscriber)
    {
        var hubType = typeof(EventHub<,,>).MakeGenericType(eventType, typeof(InMemoryEventStorageRecord), typeof(InMemoryEventHubStorage));
        return TryGetSubscriberByHubType(hubType, subscriberId, out subscriber);
    }

    static bool TryGetSubscriberByHubType(Type hubType, string subscriberId, out object? subscriber)
    {
        var field = hubType.GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Static)!;
        var dict = field.GetValue(null)!;
        var args = new object?[] { subscriberId, null };
        var found = (bool)dict.GetType().GetMethod("TryGetValue")!.Invoke(dict, args)!;
        subscriber = args[1];

        return found;
    }

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

    class RRTestEventOnlyOne : IEvent
    {
        public int EventID { get; set; }
    }

    class RRTestEventMulti : IEvent
    {
        public int EventID { get; set; }
    }

    class RRTestEventOneConnected : IEvent
    {
        public int EventID { get; set; }
    }

    class RRKnownSubscriberEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class RRConcurrentSelectionEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class RRConcurrentPublishEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class RRTestEventThreeSubs : IEvent
    {
        public int EventID { get; set; }
    }

    sealed class RoundRobinRecordingRecord : IEventStorageRecord
    {
        public string SubscriberID { get; set; } = default!;
        public Guid TrackingID { get; set; }
        public object Event { get; set; } = default!;
        public string EventType { get; set; } = default!;
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
    }

    sealed class RoundRobinRecordingStorageState
    {
        readonly Lock _lock = new();
        readonly List<RoundRobinRecordingRecord> _records = [];

        public void Store(IEnumerable<RoundRobinRecordingRecord> records)
        {
            lock (_lock)
                _records.AddRange(records.Select(Clone));
        }

        public IReadOnlyList<RoundRobinRecordingRecord> GetStoredRecords()
        {
            lock (_lock)
                return _records.ToArray();
        }

        static RoundRobinRecordingRecord Clone(RoundRobinRecordingRecord record)
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

    sealed class RoundRobinRecordingStorage(RoundRobinRecordingStorageState state) : IEventHubStorageProvider<RoundRobinRecordingRecord>
    {
        public ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<RoundRobinRecordingRecord> parameters)
            => new(Array.Empty<string>());

        public ValueTask StoreEventsAsync(IEnumerable<RoundRobinRecordingRecord> records, CancellationToken ct)
        {
            state.Store(records);

            return default;
        }

        public ValueTask<IEnumerable<RoundRobinRecordingRecord>> GetNextBatchAsync(PendingRecordSearchParams<RoundRobinRecordingRecord> parameters)
            => new(Array.Empty<RoundRobinRecordingRecord>());

        public ValueTask MarkEventAsCompleteAsync(RoundRobinRecordingRecord record, CancellationToken ct)
            => default;

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<RoundRobinRecordingRecord> parameters)
            => default;
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
