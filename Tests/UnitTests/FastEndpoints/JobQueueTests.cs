using FastEndpoints;
using System.Linq.Expressions;
using Xunit;

namespace JobQueue;

public class JobQueueTests
{
    // minimal record that relies on the default no-op DequeueAfter implementation
    class BasicJob : IJobStorageRecord
    {
        public string QueueID { get; set; } = "";
        public Guid TrackingID { get; set; }
        public object Command { get; set; } = null!;
        public DateTime ExecuteAfter { get; set; }
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
    }

    // distributed aware record that overrides DequeueAfter with a real backing field
    class DistributedJob : IJobStorageRecord
    {
        public string QueueID { get; set; } = "";
        public Guid TrackingID { get; set; }
        public object Command { get; set; } = null!;
        public DateTime ExecuteAfter { get; set; }
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
        public DateTime DequeueAfter { get; set; }
    }

    // distributed aware storage provider that atomically claims jobs (simulates database level atomic operations)
    class DistributedJobStorage : IJobStorageProvider<DistributedJob>
    {
        readonly Lock _lock = new();
        readonly List<DistributedJob> _jobs = [];

        public bool DistributedJobProcessingEnabled => true;
        public List<DistributedJob> Jobs => _jobs;

        public Task StoreJobAsync(DistributedJob r, CancellationToken ct)
        {
            lock (_lock)
                _jobs.Add(r);

            return Task.CompletedTask;
        }

        public Task<ICollection<DistributedJob>> GetNextBatchAsync(PendingJobSearchParams<DistributedJob> p)
        {
            var match = p.Match.Compile();
            var now = DateTime.UtcNow;
            var leaseTime = p.ExecutionTimeLimit == Timeout.InfiniteTimeSpan
                                ? TimeSpan.FromMinutes(5)
                                : p.ExecutionTimeLimit;

            lock (_lock)
            {
                // atomically find and claim jobs (simulates UPDATE...RETURNING / FindOneAndUpdate)
                // p.Match already includes the DequeueAfter <= now check
                var results = _jobs
                              .Where(match)
                              .OrderBy(r => r.TrackingID)
                              .Take(p.Limit)
                              .ToArray();

                // claim them by setting DequeueAfter to the future
                foreach (var job in results)
                    job.DequeueAfter = now + leaseTime;

                return Task.FromResult<ICollection<DistributedJob>>(results);
            }
        }

        public Task MarkJobAsCompleteAsync(DistributedJob r, CancellationToken ct)
        {
            lock (_lock)
                r.IsComplete = true;

            return Task.CompletedTask;
        }

        public Task CancelJobAsync(Guid trackingId, CancellationToken ct)
        {
            lock (_lock)
            {
                var j = _jobs.Single(j => j.TrackingID == trackingId);
                j.IsComplete = true;
            }

            return Task.CompletedTask;
        }

        public Task OnHandlerExecutionFailureAsync(DistributedJob r, Exception exception, CancellationToken ct)
        {
            lock (_lock)

                // reset the lease so the job can be picked up again by any worker
                r.DequeueAfter = DateTime.MinValue;

            return Task.CompletedTask;
        }

        public Task PurgeStaleJobsAsync(StaleJobSearchParams<DistributedJob> parameters)
            => Task.CompletedTask;
    }

    //DequeueAfter default implementation
    [Fact]
    public void default_dequeue_after_getter_returns_datetime_min_value()
    {
        IJobStorageRecord record = new BasicJob();
        record.DequeueAfter.ShouldBe(default);
    }

    [Fact]
    public void default_dequeue_after_setter_is_no_op()
    {
        IJobStorageRecord record = new BasicJob();
        record.DequeueAfter = DateTime.UtcNow.AddHours(1);
        record.DequeueAfter.ShouldBe(default);
    }

    //real DequeueAfter implementation
    [Fact]
    public void overridden_dequeue_after_getter_returns_set_value()
    {
        var future = DateTime.UtcNow.AddMinutes(10);
        var record = new DistributedJob { DequeueAfter = future };
        record.DequeueAfter.ShouldBe(future);
    }

    [Fact]
    public void overridden_dequeue_after_setter_persists_value()
    {
        var record = new DistributedJob();
        record.DequeueAfter.ShouldBe(default);

        var future = DateTime.UtcNow.AddMinutes(30);
        record.DequeueAfter = future;
        record.DequeueAfter.ShouldBe(future);
    }

    //PendingJobSearchParams.ExecutionTimeLimit
    [Fact]
    public void execution_time_limit_can_be_set_and_retrieved()
    {
        var p = new PendingJobSearchParams<BasicJob>
        {
            ExecutionTimeLimit = TimeSpan.FromMinutes(5)
        };

        p.ExecutionTimeLimit.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void execution_time_limit_defaults_to_zero()
    {
        var p = new PendingJobSearchParams<BasicJob>();
        p.ExecutionTimeLimit.ShouldBe(TimeSpan.Zero);
    }

    //distributed storage provider (atomic claiming)
    [Fact]
    public async Task atomic_claiming_prevents_duplicate_processing()
    {
        var storage = new DistributedJobStorage();
        var now = DateTime.UtcNow;

        // create 5 pending jobs
        for (var i = 0; i < 5; i++)
        {
            await storage.StoreJobAsync(
                new()
                {
                    QueueID = "test-queue",
                    TrackingID = Guid.NewGuid(),
                    Command = $"cmd-{i}",
                    ExecuteAfter = now.AddMinutes(-1),
                    ExpireOn = now.AddHours(1),
                    DequeueAfter = DateTime.MinValue // eligible immediately
                },
                default);
        }

        Expression<Func<DistributedJob, bool>> match =
            r => r.QueueID == "test-queue" &&
                 !r.IsComplete &&
                 r.ExecuteAfter <= now &&
                 r.ExpireOn >= now &&
                 r.DequeueAfter <= now;

        var searchParams = new PendingJobSearchParams<DistributedJob>
        {
            QueueID = "test-queue",
            Match = match,
            Limit = 5,
            ExecutionTimeLimit = TimeSpan.FromMinutes(10)
        };

        // worker 1 claims all 5 jobs
        var batch1 = await storage.GetNextBatchAsync(searchParams);
        batch1.Count.ShouldBe(5);

        // worker 2 tries to claim. should get 0 because all are leased.
        var batch2 = await storage.GetNextBatchAsync(searchParams);
        batch2.Count.ShouldBe(0);
    }

    [Fact]
    public async Task concurrent_workers_do_not_get_duplicate_jobs()
    {
        var storage = new DistributedJobStorage();
        var now = DateTime.UtcNow;

        // create 10 pending jobs
        for (var i = 0; i < 10; i++)
        {
            await storage.StoreJobAsync(
                new()
                {
                    QueueID = "test-queue",
                    TrackingID = Guid.NewGuid(),
                    Command = $"cmd-{i}",
                    ExecuteAfter = now.AddMinutes(-1),
                    ExpireOn = now.AddHours(1),
                    DequeueAfter = DateTime.MinValue
                },
                CancellationToken.None);
        }

        Expression<Func<DistributedJob, bool>> match =
            r => r.QueueID == "test-queue" &&
                 !r.IsComplete &&
                 r.ExecuteAfter <= now &&
                 r.ExpireOn >= now &&
                 r.DequeueAfter <= now;

        // simulate 5 concurrent workers each requesting a batch of 3
        var tasks = Enumerable.Range(0, 5)
                              .Select(
                                  _ => storage.GetNextBatchAsync(
                                      new()
                                      {
                                          QueueID = "test-queue",
                                          Match = match,
                                          Limit = 3,
                                          ExecutionTimeLimit = TimeSpan.FromMinutes(10)
                                      }));

        var results = await Task.WhenAll(tasks);
        var allClaimedIds = results.SelectMany(r => r.Select(j => j.TrackingID)).ToList();

        // no duplicates
        allClaimedIds.Count.ShouldBe(allClaimedIds.Distinct().Count());

        // all 10 jobs should be claimed across the workers
        allClaimedIds.Count.ShouldBe(10);
    }

    // lease expiry (crash recovery)
    [Fact]
    public async Task expired_lease_makes_job_available_again()
    {
        var storage = new DistributedJobStorage();
        var now = DateTime.UtcNow;

        // create a job with a very short lease time (already expired)
        var job = new DistributedJob
        {
            QueueID = "test-queue",
            TrackingID = Guid.NewGuid(),
            Command = "crash-test",
            ExecuteAfter = now.AddMinutes(-1),
            ExpireOn = now.AddHours(1),
            DequeueAfter = now.AddSeconds(-1) // lease already expired
        };
        await storage.StoreJobAsync(job, CancellationToken.None);

        Expression<Func<DistributedJob, bool>> match =
            r => r.QueueID == "test-queue" &&
                 !r.IsComplete &&
                 r.ExecuteAfter <= now &&
                 r.ExpireOn >= now &&
                 r.DequeueAfter <= now;

        var searchParams = new PendingJobSearchParams<DistributedJob>
        {
            QueueID = "test-queue",
            Match = match,
            Limit = 1,
            ExecutionTimeLimit = TimeSpan.FromMinutes(10)
        };

        // should be able to pick up the job since its lease expired
        var batch = await storage.GetNextBatchAsync(searchParams);
        batch.Count.ShouldBe(1);
        batch.First().TrackingID.ShouldBe(job.TrackingID);
    }

    [Fact]
    public async Task active_lease_prevents_job_pickup()
    {
        var storage = new DistributedJobStorage();
        var now = DateTime.UtcNow;

        // create a job with a lease that's still active (far in the future)
        var job = new DistributedJob
        {
            QueueID = "test-queue",
            TrackingID = Guid.NewGuid(),
            Command = "leased-job",
            ExecuteAfter = now.AddMinutes(-1),
            ExpireOn = now.AddHours(1),
            DequeueAfter = now.AddMinutes(30) // lease still active
        };
        await storage.StoreJobAsync(job, default);

        Expression<Func<DistributedJob, bool>> match =
            r => r.QueueID == "test-queue" &&
                 !r.IsComplete &&
                 r.ExecuteAfter <= now &&
                 r.ExpireOn >= now &&
                 r.DequeueAfter <= now;

        var searchParams = new PendingJobSearchParams<DistributedJob>
        {
            QueueID = "test-queue",
            Match = match,
            Limit = 1,
            ExecutionTimeLimit = TimeSpan.FromMinutes(10)
        };

        // should not be able to pick up the job because lease is still active
        var batch = await storage.GetNextBatchAsync(searchParams);
        batch.Count.ShouldBe(0);
    }

    // execution failure (DequeueAfter reset)
    [Fact]
    public async Task on_handler_failure_resets_dequeue_after_making_job_available()
    {
        var storage = new DistributedJobStorage();
        var now = DateTime.UtcNow;

        var job = new DistributedJob
        {
            QueueID = "test-queue",
            TrackingID = Guid.NewGuid(),
            Command = "fail-job",
            ExecuteAfter = now.AddMinutes(-1),
            ExpireOn = now.AddHours(1),
            DequeueAfter = DateTime.MinValue
        };
        await storage.StoreJobAsync(job, default);

        Expression<Func<DistributedJob, bool>> match =
            r => r.QueueID == "test-queue" &&
                 !r.IsComplete &&
                 r.ExecuteAfter <= now &&
                 r.ExpireOn >= now &&
                 r.DequeueAfter <= now;

        var searchParams = new PendingJobSearchParams<DistributedJob>
        {
            QueueID = "test-queue",
            Match = match,
            Limit = 1,
            ExecutionTimeLimit = TimeSpan.FromMinutes(10)
        };

        // worker picks up the job (lease is set)
        var batch1 = await storage.GetNextBatchAsync(searchParams);
        batch1.Count.ShouldBe(1);

        // job is now leased. another worker can't pick it up
        var batch2 = await storage.GetNextBatchAsync(searchParams);
        batch2.Count.ShouldBe(0);

        // handler fails. provider resets DequeueAfter.
        await storage.OnHandlerExecutionFailureAsync(job, new InvalidOperationException("test failure"), default);

        // now the job should be available again
        var batch3 = await storage.GetNextBatchAsync(searchParams);
        batch3.Count.ShouldBe(1);
        batch3.First().TrackingID.ShouldBe(job.TrackingID);
    }

    // completed jobs are not reclaimed
    [Fact]
    public async Task completed_jobs_are_not_returned_by_get_next_batch()
    {
        var storage = new DistributedJobStorage();
        var now = DateTime.UtcNow;

        var job = new DistributedJob
        {
            QueueID = "test-queue",
            TrackingID = Guid.NewGuid(),
            Command = "done-job",
            ExecuteAfter = now.AddMinutes(-1),
            ExpireOn = now.AddHours(1),
            DequeueAfter = DateTime.MinValue
        };
        await storage.StoreJobAsync(job, default);

        Expression<Func<DistributedJob, bool>> match =
            r => r.QueueID == "test-queue" &&
                 !r.IsComplete &&
                 r.ExecuteAfter <= now &&
                 r.ExpireOn >= now &&
                 r.DequeueAfter <= now;

        var searchParams = new PendingJobSearchParams<DistributedJob>
        {
            QueueID = "test-queue",
            Match = match,
            Limit = 1,
            ExecutionTimeLimit = TimeSpan.FromMinutes(10)
        };

        // claim and complete the job
        var batch = await storage.GetNextBatchAsync(searchParams);
        batch.Count.ShouldBe(1);
        await storage.MarkJobAsCompleteAsync(batch.First(), CancellationToken.None);

        // reset its lease even though it's complete
        job.DequeueAfter = DateTime.MinValue;

        // it should still not be returned because IsComplete is true (filtered by Match)
        var batch2 = await storage.GetNextBatchAsync(searchParams);
        batch2.Count.ShouldBe(0);
    }

    // mixed scenario (multiple queues)
    [Fact]
    public async Task claiming_is_scoped_to_queue_id()
    {
        var storage = new DistributedJobStorage();
        var now = DateTime.UtcNow;

        // add jobs to two different queues
        var jobA = new DistributedJob
        {
            QueueID = "queue-A",
            TrackingID = Guid.NewGuid(),
            Command = "cmd-A",
            ExecuteAfter = now.AddMinutes(-1),
            ExpireOn = now.AddHours(1),
            DequeueAfter = DateTime.MinValue
        };

        var jobB = new DistributedJob
        {
            QueueID = "queue-B",
            TrackingID = Guid.NewGuid(),
            Command = "cmd-B",
            ExecuteAfter = now.AddMinutes(-1),
            ExpireOn = now.AddHours(1),
            DequeueAfter = DateTime.MinValue
        };

        await storage.StoreJobAsync(jobA, default);
        await storage.StoreJobAsync(jobB, default);

        // claim from queue-A
        Expression<Func<DistributedJob, bool>> matchA =
            r => r.QueueID == "queue-A" &&
                 !r.IsComplete &&
                 r.ExecuteAfter <= now &&
                 r.ExpireOn >= now &&
                 r.DequeueAfter <= now;

        var batchA = await storage.GetNextBatchAsync(
                         new()
                         {
                             QueueID = "queue-A",
                             Match = matchA,
                             Limit = 5,
                             ExecutionTimeLimit = TimeSpan.FromMinutes(10)
                         });

        batchA.Count.ShouldBe(1);
        batchA.First().QueueID.ShouldBe("queue-A");

        // queue-B job should still be available
        Expression<Func<DistributedJob, bool>> matchB =
            r => r.QueueID == "queue-B" &&
                 !r.IsComplete &&
                 r.ExecuteAfter <= now &&
                 r.ExpireOn >= now &&
                 r.DequeueAfter <= now;

        var batchB = await storage.GetNextBatchAsync(
                         new()
                         {
                             QueueID = "queue-B",
                             Match = matchB,
                             Limit = 5,
                             ExecutionTimeLimit = TimeSpan.FromMinutes(10)
                         });

        batchB.Count.ShouldBe(1);
        batchB.First().QueueID.ShouldBe("queue-B");
    }

    // backward compatibility
    [Fact]
    public async Task non_distributed_provider_works_without_dequeue_after()
    {
        // this simulates the existing non-distributed scenario where BasicJob uses the default no-op DequeueAfter.
        // the provider doesn't check DequeueAfter at all.
        var jobs = new List<BasicJob>();
        var now = DateTime.UtcNow;

        var job = new BasicJob
        {
            QueueID = "basic-queue",
            TrackingID = Guid.NewGuid(),
            Command = "basic-cmd",
            ExecuteAfter = now.AddMinutes(-1),
            ExpireOn = now.AddHours(1)
        };
        jobs.Add(job);

        Expression<Func<BasicJob, bool>> match =
            r => r.QueueID == "basic-queue" &&
                 !r.IsComplete &&
                 r.ExecuteAfter <= now &&
                 r.ExpireOn >= now;

        var compiled = match.Compile();

        // non-distributed provider just filters by the Match expression (no DequeueAfter check)
        var result = jobs.Where(compiled).Take(1).ToArray();
        result.Length.ShouldBe(1);

        // verify DequeueAfter is still default and doesn't interfere
        ((IJobStorageRecord)result[0]).DequeueAfter.ShouldBe(default);
    }

    // future-scheduled jobs are not prematurely claimed
    [Fact]
    public async Task future_scheduled_jobs_are_not_claimed_by_get_next_batch()
    {
        var storage = new DistributedJobStorage();
        var now = DateTime.UtcNow;

        // create a future-scheduled job
        var job = new DistributedJob
        {
            QueueID = "test-queue",
            TrackingID = Guid.NewGuid(),
            Command = "future-cmd",
            ExecuteAfter = now.AddMinutes(30), // 30 minutes in the future
            ExpireOn = now.AddHours(2),
            DequeueAfter = DateTime.MinValue
        };
        await storage.StoreJobAsync(job, default);

        // the match expression (as the engine would produce) excludes future jobs and includes DequeueAfter check
        Expression<Func<DistributedJob, bool>> match =
            r => r.QueueID == "test-queue" &&
                 !r.IsComplete &&
                 r.ExecuteAfter <= now &&
                 r.ExpireOn >= now &&
                 r.DequeueAfter <= now;

        var searchParams = new PendingJobSearchParams<DistributedJob>
        {
            QueueID = "test-queue",
            Match = match,
            Limit = 5,
            ExecutionTimeLimit = TimeSpan.FromMinutes(10)
        };

        // GetNextBatchAsync should return 0 (job is not yet due)
        var batch = await storage.GetNextBatchAsync(searchParams);
        batch.Count.ShouldBe(0);

        // the job's DequeueAfter should still be at its original value (not prematurely claimed)
        job.DequeueAfter.ShouldBe(DateTime.MinValue);
    }
}