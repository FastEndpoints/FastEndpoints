namespace NativeAotChecker.Endpoints.Jobs;

public sealed class JobQueueRequest
{
    [RouteParam]
    public Guid Id { get; set; }
}

public sealed class JobQueueEndpoint : Endpoint<JobQueueRequest, string>
{
    public override void Configure()
    {
        Get("job-queue/{id:guid}");
        AllowAnonymous();
    }

    public override async Task<string> ExecuteAsync(JobQueueRequest req, CancellationToken ct)
    {
        var cmd = new EchoGuidCommand { Id = req.Id };
        var trackingId = await cmd.QueueJobAsync(ct: ct);

        string? result = null;

        while (!ct.IsCancellationRequested && result is null)
        {
            result = await JobTracker<EchoGuidCommand>.GetJobResultAsync<string>(trackingId, ct);
            if (result is null)
                await Task.Delay(50, ct);
        }

        return result!;
    }
}

public sealed class EchoGuidCommand : ICommand<string>
{
    public Guid Id { get; init; }
}

public sealed class EchoGuidCommandHandler : ICommandHandler<EchoGuidCommand, string>
{
    public Task<string> ExecuteAsync(EchoGuidCommand cmd, CancellationToken ct)
        => Task.FromResult(cmd.Id.ToString());
}

public sealed class Job : IJobStorageRecord, IJobResultStorage
{
    public Guid ID { get; set; } = Guid.NewGuid();
    public string QueueID { get; set; } = null!;
    public Guid TrackingID { get; set; }
    public object Command { get; set; } = null!;
    public DateTime ExecuteAfter { get; set; }
    public DateTime ExpireOn { get; set; }
    public bool IsComplete { get; set; }
    public object? Result { get; set; }
}

public sealed class JobStorage : IJobStorageProvider<Job>, IJobResultProvider
{
    static readonly List<Job> _jobs = [];
    static readonly Lock _lock = new();

    public bool DistributedJobProcessingEnabled => false;

    public Task StoreJobAsync(Job r, CancellationToken ct)
    {
        lock (_lock)
            _jobs.Add(r);

        return Task.CompletedTask;
    }

    public Task<ICollection<Job>> GetNextBatchAsync(PendingJobSearchParams<Job> p)
    {
        var match = p.Match.Compile();

        lock (_lock)
        {
            return Task.FromResult<ICollection<Job>>(
                _jobs.Where(match)
                     .OrderBy(r => r.ID)
                     .Take(p.Limit)
                     .ToArray());
        }
    }

    public Task MarkJobAsCompleteAsync(Job r, CancellationToken ct)
    {
        lock (_lock)
        {
            var j = _jobs.Single(j => j.ID == r.ID);
            j.IsComplete = true;
        }

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

    public Task OnHandlerExecutionFailureAsync(Job r, Exception exception, CancellationToken ct)
        => Task.CompletedTask;

    public Task PurgeStaleJobsAsync(StaleJobSearchParams<Job> parameters)
        => Task.CompletedTask;

    public Task StoreJobResultAsync<TResult>(Guid trackingId, TResult result, CancellationToken ct)
    {
        lock (_lock)
        {
            var j = _jobs.Single(j => j.TrackingID == trackingId);
            ((IJobResultStorage)j).SetResult(result);
        }

        return Task.CompletedTask;
    }

    public Task<TResult?> GetJobResultAsync<TResult>(Guid trackingId, CancellationToken ct)
    {
        lock (_lock)
        {
            var j = _jobs.Single(j => j.TrackingID == trackingId);
            var res = ((IJobResultStorage)j).GetResult<TResult>();

            return Task.FromResult(res);
        }
    }
}