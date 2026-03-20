using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QueueTesting;
using Xunit;
using static QueueTesting.QueueTestSupport;

namespace EventQueue;

public partial class EventQueueTests
{
    [Fact]
    public async Task event_executor_refills_a_freed_slot_without_waiting_for_the_whole_batch_when_records_have_identity()
    {
        const string subscriberId = "refill-sub";
        TestEventExecutorHandler.Reset();

        var storage = new TestEventSubscriberStorage();
        var handler = new TestEventExecutorHandler(storage);
        var provider = CreateServiceProvider(services => services.AddSingleton<IEventHandler<TrackedTestEvent>>(handler));
        var logger = GetSubscriberLogger<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>(provider);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await StoreTestEventAsync(storage, subscriberId, new TrackedTestEvent { Name = "slow" }, cts.Token);
        await StoreTestEventAsync(storage, subscriberId, new TrackedTestEvent { Name = "fast" }, cts.Token);
        await StoreTestEventAsync(storage, subscriberId, new TrackedTestEvent { Name = "third" }, cts.Token);

        var executor = EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>
            .EventExecutorTask(
                storage,
                new(0),
                new(cancellationToken: cts.Token),
                maxConcurrency: 2,
                subscriberID: subscriberId,
                logger,
                _unusedHandlerFactory,
                provider,
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
    public async Task event_executor_continues_when_the_exception_receiver_throws()
    {
        const string subscriberId = "receiver-sub";
        TestEventExecutorHandler.Reset();

        var storage = new TestEventSubscriberStorage();
        var handler = new TestEventExecutorHandler(storage);
        var provider = CreateServiceProvider(
            services =>
            {
                services.AddSingleton<IEventHandler<TrackedTestEvent>>(handler);
                services.AddSingleton<SubscriberExceptionReceiver, ThrowingSubscriberExceptionReceiver>();
            });
        var logger = GetSubscriberLogger<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>(provider);
        var receiver = provider.GetRequiredService<SubscriberExceptionReceiver>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Task? executor = null;

        EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>.HandlerExecutionRetryDelay = TimeSpan.FromMilliseconds(100);

        try
        {
            await StoreTestEventAsync(storage, subscriberId, new TrackedTestEvent { Name = "retry" }, cts.Token);

            executor = EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>
                .EventExecutorTask(
                    storage,
                    new(0),
                    new(cancellationToken: cts.Token),
                    maxConcurrency: 1,
                    subscriberID: subscriberId,
                    logger,
                    _unusedHandlerFactory,
                    provider,
                    receiver);

            await TestEventExecutorHandler.RetryObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));
            TestEventExecutorHandler.ReleaseRetry();
            await WaitUntil(() => storage.AllCompleted("retry"), timeoutMs: 2000);

            cts.Cancel();
            await WaitForCompletion(executor, timeoutMs: 5000);
            storage.GetExecutionCount("retry").ShouldBe(2);
        }
        finally
        {
            cts.Cancel();

            if (executor is not null)
                await WaitForCompletion(executor, timeoutMs: 5000);

            EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>.HandlerExecutionRetryDelay = TimeSpan.FromSeconds(5);
        }
    }

    [Fact]
    public async Task event_executor_marks_successful_durable_events_complete_during_shutdown()
    {
        const string subscriberId = "shutdown-sub";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var storage = new CancellationAwareTestEventSubscriberStorage();
        var handler = new ShutdownAfterHandleEventHandler(storage, cts);
        var provider = CreateServiceProvider(services => services.AddSingleton<IEventHandler<TrackedTestEvent>>(handler));
        var logger = GetSubscriberLogger<TrackedTestEvent, ShutdownAfterHandleEventHandler, TestEventRecord, CancellationAwareTestEventSubscriberStorage>(provider);

        await StoreTestEventAsync(storage, subscriberId, new TrackedTestEvent { Name = "shutdown" }, cts.Token);

        var executor = EventSubscriber<TrackedTestEvent, ShutdownAfterHandleEventHandler, TestEventRecord, CancellationAwareTestEventSubscriberStorage>
            .EventExecutorTask(
                storage,
                new(0),
                new(cancellationToken: cts.Token),
                maxConcurrency: 1,
                subscriberID: subscriberId,
                logger,
                _unusedHandlerFactory,
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
        var logger = GetSubscriberLogger<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, CancellationAwareStoreEventSubscriberStorage>(provider);
        var receiver = EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, CancellationAwareStoreEventSubscriberStorage>
            .EventReceiverTask(
                storage,
                new(0),
                new(cancellationToken: cts.Token),
                new TestCallInvoker(new() { Name = "shutdown" }),
                CreateSubscriptionMethod<TrackedTestEvent>(),
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
        var logger = GetSubscriberLogger<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>(provider);
        var invoker = new GracefulReconnectCallInvoker(cts, expectedCalls: 3);
        var receiver = EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>
            .EventReceiverTask(
                storage,
                new(0),
                new(cancellationToken: cts.Token),
                invoker,
                CreateSubscriptionMethod<TrackedTestEvent>(),
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
        var provider = CreateServiceProvider(
            services =>
            {
                services.AddSingleton(cts);
                services.AddSingleton(observer);
                services.AddSingleton<IHostApplicationLifetime>(new TestHostLifetime(cts.Token));
            });
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
        const string subscriberId = "shared-sub";
        TestEventExecutorHandler.Reset();

        var storage = new TestEventSubscriberStorage();
        var handler = new TestEventExecutorHandler(storage);
        var provider = CreateServiceProvider(services => services.AddSingleton<IEventHandler<TrackedTestEvent>>(handler));
        var logger = GetSubscriberLogger<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>(provider);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await storage.StoreEventAsync(CreateTestRecord(subscriberId, new TestEvent { EventID = 999 }), cts.Token);
        await StoreTestEventAsync(storage, subscriberId, new TrackedTestEvent { Name = "fast" }, cts.Token);

        var executor = EventSubscriber<TrackedTestEvent, TestEventExecutorHandler, TestEventRecord, TestEventSubscriberStorage>
            .EventExecutorTask(
                storage,
                new(0),
                new(cancellationToken: cts.Token),
                maxConcurrency: 1,
                subscriberID: subscriberId,
                logger,
                _unusedHandlerFactory,
                provider,
                errorReceiver: null);

        await TestEventExecutorHandler.FastStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitUntil(() => storage.AllCompleted("fast"), timeoutMs: 5000);

        cts.Cancel();
        await WaitForCompletion(executor, timeoutMs: 5000);
        storage.GetExecutionCount("fast").ShouldBe(1);
    }

    [Fact]
    public async Task event_executor_fetches_only_available_slots_for_in_memory_provider()
    {
        const string subscriberId = "fetch-limit-sub";
        InMemFetchLimitHandler.Reset();

        var storage = new BatchDequeueEventSubscriberStorage();
        var handler = new InMemFetchLimitHandler();
        var provider = CreateServiceProvider(services => services.AddSingleton<IEventHandler<InMemFetchLimitEvent>>(handler));
        var logger = GetSubscriberLogger<InMemFetchLimitEvent, InMemFetchLimitHandler, TestEventRecord, BatchDequeueEventSubscriberStorage>(provider);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        SetIsInMemorySubscriberProvider<InMemFetchLimitEvent, InMemFetchLimitHandler, TestEventRecord, BatchDequeueEventSubscriberStorage>();

        await storage.StoreEventAsync(CreateTestRecord(subscriberId, new InMemFetchLimitEvent { Name = "slow" }), cts.Token);

        for (var index = 1; index <= 4; index++)
            await storage.StoreEventAsync(CreateTestRecord(subscriberId, new InMemFetchLimitEvent { Name = $"fast-{index}" }), cts.Token);

        var executor = EventSubscriber<InMemFetchLimitEvent, InMemFetchLimitHandler, TestEventRecord, BatchDequeueEventSubscriberStorage>
            .EventExecutorTask(
                storage,
                new(0),
                new(cancellationToken: cts.Token),
                maxConcurrency: 2,
                subscriberID: subscriberId,
                logger,
                _unusedHandlerFactory,
                provider,
                errorReceiver: null);

        await WaitUntil(() => InMemFetchLimitHandler.ProcessedCount >= 4, timeoutMs: 5000);

        InMemFetchLimitHandler.ReleaseSlow();

        await WaitUntil(() => InMemFetchLimitHandler.ProcessedCount == 5, timeoutMs: 5000);

        InMemFetchLimitHandler.ProcessedCount.ShouldBe(5);
        storage.GetRequestedLimitsSnapshot().ShouldContain(1);

        cts.Cancel();
        await WaitForCompletion(executor, timeoutMs: 5000);
    }
}
