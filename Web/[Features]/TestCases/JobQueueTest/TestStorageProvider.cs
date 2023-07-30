namespace TestCases.JobQueueTest;

public class JobRecord : IJobStorageRecord
{
    public Guid ID { get; set; } = Guid.NewGuid();

    public string QueueID { get; set; }
    public object Command { get; set; }
    public DateTime ExecuteAfter { get; set; }
    public DateTime ExpireOn { get; set; }
    public bool IsComplete { get; set; }
}

public class JobProvider : IJobStorageProvider<JobRecord>
{
    private readonly List<JobRecord> jobs = new();

    public Task<IEnumerable<JobRecord>> GetNextBatchAsync(PendingJobSearchParams<JobRecord> p)
    {
        var match = p.Match.Compile();
        return Task.FromResult(jobs
            .Where(match)
            .OrderBy(r => r.ID)
            .Take(p.Limit));
    }

    public Task MarkJobAsCompleteAsync(JobRecord r, CancellationToken ct)
    {

    }

    public Task OnHandlerExecutionFailureAsync(JobRecord r, Exception exception, CancellationToken ct) => throw new NotImplementedException();
    public Task PurgeStaleJobsAsync(StaleJobSearchParams<JobRecord> parameters) => throw new NotImplementedException();
    public Task StoreJobAsync(JobRecord r, CancellationToken ct) => throw new NotImplementedException();
}
