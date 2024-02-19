namespace TestCases.JobQueueTest;

public class Job : IJobStorageRecord
{
    public Guid ID { get; set; } = Guid.NewGuid();

    public string QueueID { get; set; }
    public object Command { get; set; }
    public DateTime ExecuteAfter { get; set; }
    public DateTime ExpireOn { get; set; }
    public bool IsComplete { get; set; }
}

public class JobStorage : IJobStorageProvider<Job>
{
    readonly List<Job> jobs = new();

    public Task StoreJobAsync(Job r, CancellationToken ct)
    {
        jobs.Add(r);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<Job>> GetNextBatchAsync(PendingJobSearchParams<Job> p)
    {
        var match = p.Match.Compile();
        return Task.FromResult(jobs
            .Where(match)
            .OrderBy(r => r.ID)
            .Take(p.Limit));
    }

    public Task MarkJobAsCompleteAsync(Job r, CancellationToken ct)
    {
        var j = jobs.Single(j => j.ID == r.ID);
        j.IsComplete = true;
        return Task.CompletedTask;
    }

    public Task OnHandlerExecutionFailureAsync(Job r, Exception exception, CancellationToken ct) => Task.CompletedTask;

    public Task PurgeStaleJobsAsync(StaleJobSearchParams<Job> parameters) => Task.CompletedTask;
}
