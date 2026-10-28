using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints.Jobs;

public sealed class VoidJobQueueRequest
{
    [RouteParam]
    public bool Fail { get; set; }
}

public sealed class VoidJobQueueResponse
{
    public bool IsComplete { get; set; }
    public bool FailureRecorded { get; set; }
    public int StoreAttempts { get; set; }
    public int GetAttempts { get; set; }
}

public sealed class VoidJobQueueEndpoint : Endpoint<VoidJobQueueRequest, VoidJobQueueResponse>
{
    public override void Configure()
    {
        Get("job-queue-void/{fail:bool}");
        AllowAnonymous();
    }

    public override async Task<VoidJobQueueResponse> ExecuteAsync(VoidJobQueueRequest req, CancellationToken ct)
    {
        var trackingId = await new VoidJobCommand { Fail = req.Fail }.QueueJobAsync(ct: ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while (true)
        {
            var status = JobStorage.GetVoidJobStatus(trackingId);
            if (status.IsComplete)
                return status;

            await Task.Delay(20, timeout.Token);
        }
    }
}

public sealed class VoidJobCommand : ICommand
{
    public bool Fail { get; init; }
}

public sealed class VoidJobCommandHandler : ICommandHandler<VoidJobCommand>
{
    public Task ExecuteAsync(VoidJobCommand command, CancellationToken ct)
        => command.Fail
               ? Task.FromException(new InvalidOperationException("simulated void job failure"))
               : Task.CompletedTask;
}

[JsonSerializable(typeof(FastEndpoints.Void))]
[JsonSerializable(typeof(string))]
internal partial class JobResultContext : JsonSerializerContext;
