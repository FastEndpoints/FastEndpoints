using System.Reflection;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QueueTesting;
using Xunit;
using Void = FastEndpoints.Void;

namespace JobQueue;

public partial class JobQueueTests
{
    [Fact]
    public void idempotent_via_null_selector_throws()
    {
        var opts = new JobQueueOptions();

        Should.Throw<ArgumentNullException>(() => opts.IdempotencyKeyFor<IdempotentTestCommand>(null!));
    }

    [Fact]
    public void idempotent_via_normalizes_null_empty_and_whitespace()
    {
        var opts = new JobQueueOptions();
        opts.IdempotencyKeyFor<IdempotentTestCommand>(c => c.OrderId);
        opts.TryGetIdempotencyExtractor(typeof(IdempotentTestCommand), out var extractor).ShouldBeTrue();

        extractor(new IdempotentTestCommand { OrderId = null! }).ShouldBeNull();
        extractor(new IdempotentTestCommand { OrderId = "" }).ShouldBeNull();
        extractor(new IdempotentTestCommand { OrderId = "   " }).ShouldBeNull();
        extractor(new IdempotentTestCommand { OrderId = "order-1" }).ShouldBe("order-1");
    }

    [Fact]
    public async Task queue_job_sets_idempotency_key_and_returns_new_tracking_id()
    {
        IdempotentTestHandler.Reset();
        var storage = new IdempotentTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateIdempotentQueue(storage, appStopping);
        ConfigureIdempotency(queue);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var trackingId = await new IdempotentTestCommand { OrderId = "ord-1" }.QueueJobAsync();

        trackingId.ShouldNotBe(Guid.Empty);
        var snapshot = storage.Snapshot();
        snapshot.Count.ShouldBe(1);
        snapshot[0].TrackingID.ShouldBe(trackingId);
        snapshot[0].IdempotencyKey.ShouldBe("ord-1");

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task duplicate_queue_returns_existing_tracking_id_and_does_not_store_second_row()
    {
        IdempotentTestHandler.Reset();
        var storage = new IdempotentTestStorage();
        using var appStopping = new CancellationTokenSource();
        var logger = new TestLogger<JobQueue<IdempotentTestCommand, Void, IdempotentTestRecord, IdempotentTestStorage>>();
        var queue = CreateIdempotentQueue(storage, appStopping, logger);
        ConfigureIdempotency(queue);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var firstCmd = new IdempotentTestCommand { OrderId = "ord-dup", Payload = "a" };
        var secondCmd = new IdempotentTestCommand { OrderId = "ord-dup", Payload = "b" };
        var first = await firstCmd.QueueJobAsync();
        var second = await secondCmd.QueueJobAsync();

        second.ShouldBe(first);
        secondCmd.TrackingID.ShouldBe(first);
        firstCmd.TrackingID.ShouldBe(first);
        storage.Snapshot().Count.ShouldBe(1);
        storage.StoreCount.ShouldBe(1);
        logger.Entries.ShouldContain(e => e.Level == LogLevel.Warning && e.Message.Contains("ord-dup") && e.Message.Contains(first.ToString()));

        (await storage.WaitForCompletionAsync(first, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        IdempotentTestHandler.ExecutionCount.ShouldBe(1);

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task different_keys_are_both_stored()
    {
        var storage = new IdempotentTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateIdempotentQueue(storage, appStopping);
        ConfigureIdempotency(queue);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var first = await new IdempotentTestCommand { OrderId = "ord-a" }.QueueJobAsync();
        var second = await new IdempotentTestCommand { OrderId = "ord-b" }.QueueJobAsync();

        first.ShouldNotBe(second);
        storage.Snapshot().Count.ShouldBe(2);

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task same_business_key_on_different_command_types_is_allowed()
    {
        var shared = new SharedIdempotentStorage();
        var storageA = new SharedIdempotentPrimaryStorage(shared);
        var storageB = new IdempotentOtherStorage(shared);
        using var appStopping = new CancellationTokenSource();
        var queueA = CreateSharedIdempotentPrimaryQueue(storageA, appStopping);
        var queueB = CreateIdempotentOtherQueue(storageB, appStopping);
        ConfigureIdempotency(queueA);
        ConfigureIdempotencyOther(queueB);
        queueA.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));
        queueB.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var idA = await new IdempotentTestCommand { OrderId = "shared-key" }.QueueJobAsync();
        var idB = await new IdempotentOtherCommand { OrderId = "shared-key" }.QueueJobAsync();

        idA.ShouldNotBe(idB);
        var snapshot = shared.Snapshot();
        snapshot.Count.ShouldBe(2);
        snapshot.Select(j => j.QueueID).Distinct().Count().ShouldBe(2);
        snapshot.ShouldAllBe(j => j.IdempotencyKey == "shared-key");

        appStopping.Cancel();
        await Task.WhenAll(
            queueA.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5)),
            queueB.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task null_or_empty_key_is_not_deduped()
    {
        var storage = new IdempotentTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateIdempotentQueue(storage, appStopping);
        ConfigureIdempotency(queue);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var first = await new IdempotentTestCommand { OrderId = "" }.QueueJobAsync();
        var second = await new IdempotentTestCommand { OrderId = "   " }.QueueJobAsync();

        first.ShouldNotBe(second);
        storage.Snapshot().Count.ShouldBe(2);
        storage.Snapshot().All(j => j.IdempotencyKey is null).ShouldBeTrue();

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task completed_but_not_purged_job_still_blocks_duplicate_key()
    {
        IdempotentTestHandler.Reset();
        var storage = new IdempotentTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateIdempotentQueue(storage, appStopping);
        ConfigureIdempotency(queue);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var first = await new IdempotentTestCommand { OrderId = "completed-key" }.QueueJobAsync();
        (await storage.WaitForCompletionAsync(first, TimeSpan.FromSeconds(5))).ShouldBeTrue();

        var second = await new IdempotentTestCommand { OrderId = "completed-key" }.QueueJobAsync();

        second.ShouldBe(first);
        storage.Snapshot().Count.ShouldBe(1);

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task after_purge_same_key_can_be_queued_again()
    {
        IdempotentTestHandler.Reset();
        var storage = new IdempotentTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateIdempotentQueue(storage, appStopping);
        ConfigureIdempotency(queue);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var first = await new IdempotentTestCommand { OrderId = "purge-key" }.QueueJobAsync();
        (await storage.WaitForCompletionAsync(first, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        await storage.RemoveByTrackingIdAsync(first);

        var second = await new IdempotentTestCommand { OrderId = "purge-key" }.QueueJobAsync();

        second.ShouldNotBe(first);
        storage.Snapshot().Count.ShouldBe(1);
        storage.Snapshot()[0].TrackingID.ShouldBe(second);

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task create_job_populates_idempotency_key_without_storing()
    {
        var storage = new IdempotentTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateIdempotentQueue(storage, appStopping);
        ConfigureIdempotency(queue);

        var job = new IdempotentTestCommand { OrderId = "create-only" }.CreateJob<IdempotentTestRecord>();

        job.IdempotencyKey.ShouldBe("create-only");
        storage.Snapshot().Count.ShouldBe(0);

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task commands_without_idempotent_via_leave_key_unset()
    {
        var storage = new IdempotentTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateIdempotentQueue(storage, appStopping);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        await new IdempotentTestCommand { OrderId = "no-config" }.QueueJobAsync();

        storage.Snapshot().Single().IdempotencyKey.ShouldBeNull();

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task parallel_duplicate_queues_only_store_one_row()
    {
        var storage = new IdempotentTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateIdempotentQueue(storage, appStopping);
        ConfigureIdempotency(queue);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var ids = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => new IdempotentTestCommand { OrderId = "race-key" }.QueueJobAsync()));

        ids.Distinct().Count().ShouldBe(1);
        storage.Snapshot().Count.ShouldBe(1);

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void use_job_queues_throws_when_idempotency_configured_without_record_support()
    {
        var registry = new CommandHandlerRegistry
        {
            [typeof(IdempotentTestCommand)] = new(typeof(IdempotentTestHandler))
        };

        var services = new ServiceCollection();
        services.AddSingleton(registry);
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton(sp => (IServiceResolver)new ServiceResolver(sp, sp.GetRequiredService<IHttpContextAccessor>(), true));
        var provider = services.BuildServiceProvider();

        typeof(JobQueueExtensions)
            .GetField("_tStorageRecord", BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, typeof(IdempotentTestRecordWithoutKey));
        typeof(JobQueueExtensions)
            .GetField("_tStorageProvider", BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, typeof(IdempotentNoKeyStorage));

        var ex = Should.Throw<InvalidOperationException>(() => provider.UseJobQueues(o => o.IdempotencyKeyFor<IdempotentTestCommand>(c => c.OrderId)));

        ex.Message.ShouldContain(nameof(IHasIdempotencyKey));
    }

    [Fact]
    public void duplicate_job_exception_with_empty_tracking_id_is_rejected()
    {
        var storage = new ThrowingEmptyTrackingStorage();
        using var appStopping = new CancellationTokenSource();
        Factory.RegisterTestServices(_ => { });
        new IdempotentTestHandler().RegisterForTesting();

        var queue = new JobQueue<IdempotentTestCommand, Void, IdempotentTestRecord, ThrowingEmptyTrackingStorage>(
            storage,
            new TestHostLifetime(appStopping.Token),
            NullLogger<JobQueue<IdempotentTestCommand, Void, IdempotentTestRecord, ThrowingEmptyTrackingStorage>>.Instance);

        ConfigureIdempotency(queue);

        var ex = Should.Throw<InvalidOperationException>(() => new IdempotentTestCommand { OrderId = "x" }.QueueJobAsync().GetAwaiter().GetResult());

        ex.Message.ShouldContain(nameof(DuplicateJobException.ExistingTrackingID));
        appStopping.Cancel();
    }

    sealed class ThrowingEmptyTrackingStorage : IJobStorageProvider<IdempotentTestRecord>
    {
        public bool DistributedJobProcessingEnabled => false;

        public Task StoreJobAsync(IdempotentTestRecord record, CancellationToken ct)
            => throw new DuplicateJobException(Guid.Empty, record.IdempotencyKey, record.QueueID);

        public Task<ICollection<IdempotentTestRecord>> GetNextBatchAsync(PendingJobSearchParams<IdempotentTestRecord> parameters)
            => Task.FromResult<ICollection<IdempotentTestRecord>>([]);

        public Task MarkJobAsCompleteAsync(IdempotentTestRecord record, CancellationToken ct)
            => Task.CompletedTask;

        public Task CancelJobAsync(Guid trackingId, CancellationToken ct)
            => Task.CompletedTask;

        public Task OnHandlerExecutionFailureAsync(IdempotentTestRecord record, Exception exception, CancellationToken ct)
            => Task.CompletedTask;

        public Task PurgeStaleJobsAsync(StaleJobSearchParams<IdempotentTestRecord> parameters)
            => Task.CompletedTask;
    }
}