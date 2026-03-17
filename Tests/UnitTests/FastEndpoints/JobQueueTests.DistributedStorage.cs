using FastEndpoints;
using System.Linq.Expressions;
using Xunit;

namespace JobQueue;

public partial class JobQueueTests
{
    sealed class DistributedJobStorage : IJobStorageProvider<DistributedJob>
    {
        readonly Lock _lock = new();
        readonly List<DistributedJob> _jobs = [];

        public bool DistributedJobProcessingEnabled => true;

        public Task StoreJobAsync(DistributedJob record, CancellationToken ct)
        {
            lock (_lock)
                _jobs.Add(record);

            return Task.CompletedTask;
        }

        public Task<ICollection<DistributedJob>> GetNextBatchAsync(PendingJobSearchParams<DistributedJob> parameters)
        {
            var match = parameters.Match.Compile();
            var now = DateTime.UtcNow;
            var leaseTime = parameters.ExecutionTimeLimit == Timeout.InfiniteTimeSpan
                                ? TimeSpan.FromMinutes(5)
                                : parameters.ExecutionTimeLimit;

            lock (_lock)
            {
                var results = _jobs
                              .Where(match)
                              .OrderBy(job => job.TrackingID)
                              .Take(parameters.Limit)
                              .ToArray();

                foreach (var job in results)
                    job.DequeueAfter = now + leaseTime;

                return Task.FromResult<ICollection<DistributedJob>>(results);
            }
        }

        public Task MarkJobAsCompleteAsync(DistributedJob record, CancellationToken ct)
        {
            lock (_lock)
                record.IsComplete = true;

            return Task.CompletedTask;
        }

        public Task CancelJobAsync(Guid trackingId, CancellationToken ct)
        {
            lock (_lock)
                _jobs.Single(job => job.TrackingID == trackingId).IsComplete = true;

            return Task.CompletedTask;
        }

        public Task OnHandlerExecutionFailureAsync(DistributedJob record, Exception exception, CancellationToken ct)
        {
            lock (_lock)
                record.DequeueAfter = DateTime.MinValue;

            return Task.CompletedTask;
        }

        public Task PurgeStaleJobsAsync(StaleJobSearchParams<DistributedJob> parameters)
            => Task.CompletedTask;
    }

    static DistributedJob CreateDistributedJob(DateTime now,
                                               string queueId = "test-queue",
                                               string command = "cmd",
                                               DateTime? executeAfter = null,
                                               DateTime? expireOn = null,
                                               DateTime? dequeueAfter = null)
        => new()
        {
            QueueID = queueId,
            TrackingID = Guid.NewGuid(),
            Command = command,
            ExecuteAfter = executeAfter ?? now.AddMinutes(-1),
            ExpireOn = expireOn ?? now.AddHours(1),
            DequeueAfter = dequeueAfter ?? DateTime.MinValue
        };

    static PendingJobSearchParams<DistributedJob> CreateDistributedSearchParams(DateTime now, string queueId = "test-queue", int limit = 1)
        => new()
        {
            QueueID = queueId,
            Match = PendingDistributedJobs(queueId, now),
            Limit = limit,
            ExecutionTimeLimit = TimeSpan.FromMinutes(10)
        };

    static Expression<Func<DistributedJob, bool>> PendingDistributedJobs(string queueId, DateTime now)
        => job => job.QueueID == queueId &&
                  !job.IsComplete &&
                  job.ExecuteAfter <= now &&
                  job.ExpireOn >= now &&
                  job.DequeueAfter <= now;

    static async Task StoreJobsAsync(DistributedJobStorage storage, params DistributedJob[] jobs)
    {
        foreach (var job in jobs)
            await storage.StoreJobAsync(job, CancellationToken.None);
    }

    [Fact]
    public async Task atomic_claiming_prevents_duplicate_processing()
    {
        var storage = new DistributedJobStorage();
        var now = DateTime.UtcNow;

        await StoreJobsAsync(
            storage,
            Enumerable.Range(0, 5)
                      .Select(index => CreateDistributedJob(now, command: $"cmd-{index}"))
                      .ToArray());

        var searchParams = CreateDistributedSearchParams(now, limit: 5);
        var firstBatch = await storage.GetNextBatchAsync(searchParams);
        var secondBatch = await storage.GetNextBatchAsync(searchParams);

        firstBatch.Count.ShouldBe(5);
        secondBatch.Count.ShouldBe(0);
    }

    [Fact]
    public async Task concurrent_workers_do_not_get_duplicate_jobs()
    {
        var storage = new DistributedJobStorage();
        var now = DateTime.UtcNow;

        await StoreJobsAsync(
            storage,
            Enumerable.Range(0, 10)
                      .Select(index => CreateDistributedJob(now, command: $"cmd-{index}"))
                      .ToArray());

        var batches = await Task.WhenAll(
                          Enumerable.Range(0, 5)
                                    .Select(_ => storage.GetNextBatchAsync(CreateDistributedSearchParams(now, limit: 3))));

        var claimedIds = batches.SelectMany(batch => batch.Select(job => job.TrackingID)).ToList();

        claimedIds.Count.ShouldBe(claimedIds.Distinct().Count());
        claimedIds.Count.ShouldBe(10);
    }

    [Fact]
    public async Task expired_lease_makes_job_available_again()
    {
        var storage = new DistributedJobStorage();
        var now = DateTime.UtcNow;
        var job = CreateDistributedJob(now, command: "crash-test", dequeueAfter: now.AddSeconds(-1));

        await storage.StoreJobAsync(job, CancellationToken.None);

        var batch = await storage.GetNextBatchAsync(CreateDistributedSearchParams(now));

        batch.Count.ShouldBe(1);
        batch.First().TrackingID.ShouldBe(job.TrackingID);
    }

    [Fact]
    public async Task active_lease_prevents_job_pickup()
    {
        var storage = new DistributedJobStorage();
        var now = DateTime.UtcNow;
        var job = CreateDistributedJob(now, command: "leased-job", dequeueAfter: now.AddMinutes(30));

        await storage.StoreJobAsync(job, CancellationToken.None);

        var batch = await storage.GetNextBatchAsync(CreateDistributedSearchParams(now));

        batch.Count.ShouldBe(0);
    }

    [Fact]
    public async Task on_handler_failure_resets_dequeue_after_making_job_available()
    {
        var storage = new DistributedJobStorage();
        var now = DateTime.UtcNow;
        var job = CreateDistributedJob(now, command: "fail-job");

        await storage.StoreJobAsync(job, CancellationToken.None);

        var searchParams = CreateDistributedSearchParams(now);
        var firstBatch = await storage.GetNextBatchAsync(searchParams);
        var secondBatch = await storage.GetNextBatchAsync(searchParams);

        firstBatch.Count.ShouldBe(1);
        secondBatch.Count.ShouldBe(0);

        await storage.OnHandlerExecutionFailureAsync(job, new InvalidOperationException("test failure"), CancellationToken.None);

        var thirdBatch = await storage.GetNextBatchAsync(searchParams);

        thirdBatch.Count.ShouldBe(1);
        thirdBatch.First().TrackingID.ShouldBe(job.TrackingID);
    }

    [Fact]
    public async Task completed_jobs_are_not_returned_by_get_next_batch()
    {
        var storage = new DistributedJobStorage();
        var now = DateTime.UtcNow;
        var job = CreateDistributedJob(now, command: "done-job");

        await storage.StoreJobAsync(job, CancellationToken.None);

        var searchParams = CreateDistributedSearchParams(now);
        var batch = await storage.GetNextBatchAsync(searchParams);

        batch.Count.ShouldBe(1);
        await storage.MarkJobAsCompleteAsync(batch.First(), CancellationToken.None);

        job.DequeueAfter = DateTime.MinValue;

        var secondBatch = await storage.GetNextBatchAsync(searchParams);

        secondBatch.Count.ShouldBe(0);
    }

    [Fact]
    public async Task claiming_is_scoped_to_queue_id()
    {
        var storage = new DistributedJobStorage();
        var now = DateTime.UtcNow;
        var queueAJob = CreateDistributedJob(now, queueId: "queue-A", command: "cmd-A");
        var queueBJob = CreateDistributedJob(now, queueId: "queue-B", command: "cmd-B");

        await StoreJobsAsync(storage, queueAJob, queueBJob);

        var queueABatch = await storage.GetNextBatchAsync(CreateDistributedSearchParams(now, queueId: "queue-A", limit: 5));
        var queueBBatch = await storage.GetNextBatchAsync(CreateDistributedSearchParams(now, queueId: "queue-B", limit: 5));

        queueABatch.Count.ShouldBe(1);
        queueABatch.First().QueueID.ShouldBe("queue-A");
        queueBBatch.Count.ShouldBe(1);
        queueBBatch.First().QueueID.ShouldBe("queue-B");
    }

    [Fact]
    public async Task future_scheduled_jobs_are_not_claimed_by_get_next_batch()
    {
        var storage = new DistributedJobStorage();
        var now = DateTime.UtcNow;
        var job = CreateDistributedJob(now, command: "future-cmd", executeAfter: now.AddMinutes(30), expireOn: now.AddHours(2));

        await storage.StoreJobAsync(job, CancellationToken.None);

        var batch = await storage.GetNextBatchAsync(CreateDistributedSearchParams(now, limit: 5));

        batch.Count.ShouldBe(0);
        job.DequeueAfter.ShouldBe(DateTime.MinValue);
    }
}