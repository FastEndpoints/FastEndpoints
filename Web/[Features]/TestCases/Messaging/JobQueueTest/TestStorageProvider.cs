namespace TestCases.JobQueueTest;

public class Job : IJobStorageRecord, IJobResultStorage
{
    public Guid ID { get; set; } = Guid.NewGuid();
    public string QueueID { get; set; }
    public Guid TrackingID { get; set; }
    public object Command { get; set; }
    public DateTime ExecuteAfter { get; set; }
    public DateTime ExpireOn { get; set; }
    public bool IsComplete { get; set; }
    public object? Result { get; set; }
}

public class JobStorage : IJobStorageProvider<Job>, IJobResultProvider
{
    public static readonly List<Job> Jobs = [];

    static readonly Lock _lock = new();

    public Task StoreJobAsync(Job r, CancellationToken ct)
    {
        lock (_lock)
            Jobs.Add(r);

        return Task.CompletedTask;
    }

    public Task<IEnumerable<Job>> GetNextBatchAsync(PendingJobSearchParams<Job> p)
    {
        var match = p.Match.Compile();

        return Task.FromResult(
            Jobs.Where(match)
                .OrderBy(r => r.ID)
                .Take(p.Limit));
    }

    public Task MarkJobAsCompleteAsync(Job r, CancellationToken ct)
    {
        var j = Jobs.Single(j => j.ID == r.ID);
        j.IsComplete = true;

        return Task.CompletedTask;
    }

    public Task CancelJobAsync(Guid trackingId, CancellationToken ct)
    {
        var j = Jobs.Single(j => j.TrackingID == trackingId);
        j.IsComplete = true;

        return Task.CompletedTask;
    }

    public Task OnHandlerExecutionFailureAsync(Job r, Exception exception, CancellationToken ct)
        => Task.CompletedTask;

    public Task PurgeStaleJobsAsync(StaleJobSearchParams<Job> parameters)
        => Task.CompletedTask;

    public Task StoreJobResultAsync<TResult>(Guid trackingId, TResult result, CancellationToken ct)
    {
        var j = Jobs.Single(j => j.TrackingID == trackingId);
        ((IJobResultStorage)j).SetResult(result);

        return Task.CompletedTask;
    }

    public Task<TResult?> GetJobResultAsync<TResult>(Guid trackingId, CancellationToken ct)
    {
        var j = Jobs.Single(j => j.TrackingID == trackingId);
        var res = ((IJobResultStorage)j).GetResult<TResult>();

        return Task.FromResult(res);
    }
}