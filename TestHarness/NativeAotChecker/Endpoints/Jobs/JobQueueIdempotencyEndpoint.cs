namespace NativeAotChecker.Endpoints.Jobs;

public sealed class JobQueueIdempotencyRequest
{
    public string OrderId { get; set; } = null!;
}

public sealed class JobQueueIdempotencyResponse
{
    public Guid FirstTrackingId { get; set; }
    public Guid SecondTrackingId { get; set; }
    public string Result { get; set; } = null!;
    public int StoredCountForKey { get; set; }
    public int HandlerExecutionCount { get; set; }
}

public sealed class JobQueueIdempotencyEndpoint : Endpoint<JobQueueIdempotencyRequest, JobQueueIdempotencyResponse>
{
    public override void Configure()
    {
        Post("job-queue/idempotent");
        AllowAnonymous();
    }

    public override async Task HandleAsync(JobQueueIdempotencyRequest req, CancellationToken ct)
    {
        IdempotentEchoCommandHandler.Reset();

        var first = await new IdempotentEchoCommand { OrderId = req.OrderId, Payload = "a" }.QueueJobAsync(ct: ct);
        var second = await new IdempotentEchoCommand { OrderId = req.OrderId, Payload = "b" }.QueueJobAsync(ct: ct);

        string? result = null;

        while (!ct.IsCancellationRequested && result is null)
        {
            result = await JobTracker<IdempotentEchoCommand>.GetJobResultAsync<string>(first, ct);
            if (result is null)
                await Task.Delay(50, ct);
        }

        await Send.OkAsync(
            new JobQueueIdempotencyResponse
            {
                FirstTrackingId = first,
                SecondTrackingId = second,
                Result = result!,
                StoredCountForKey = JobStorage.CountByIdempotencyKey(req.OrderId),
                HandlerExecutionCount = IdempotentEchoCommandHandler.ExecutionCount
            },
            ct);
    }
}

public sealed class IdempotentEchoCommand : ICommand<string>
{
    public string OrderId { get; init; } = null!;
    public string Payload { get; init; } = null!;
}

public sealed class IdempotentEchoCommandHandler : ICommandHandler<IdempotentEchoCommand, string>
{
    static int _executionCount;

    public static int ExecutionCount
        => Volatile.Read(ref _executionCount);

    public static void Reset()
        => Interlocked.Exchange(ref _executionCount, 0);

    public Task<string> ExecuteAsync(IdempotentEchoCommand cmd, CancellationToken ct)
    {
        Interlocked.Increment(ref _executionCount);

        return Task.FromResult($"{cmd.OrderId}:{cmd.Payload}");
    }
}
