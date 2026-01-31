using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Timeout configuration in AOT mode
public sealed class TimeoutRequest
{
    [QueryParam]
    public int DelayMs { get; set; } = 100;
}

public sealed class TimeoutResponse
{
    public int RequestedDelayMs { get; set; }
    public int ActualDelayMs { get; set; }
    public bool Completed { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public sealed class TimeoutEndpoint : Endpoint<TimeoutRequest, TimeoutResponse>
{
    public override void Configure()
    {
        Get("timeout-test");
        AllowAnonymous();
        Options(o => o.RequireHost("*")); // Just to test Options() works
        SerializerContext<TimeoutSerCtx>();
    }

    public override async Task HandleAsync(TimeoutRequest req, CancellationToken ct)
    {
        var start = DateTime.UtcNow;
        
        await Task.Delay(req.DelayMs, ct);
        
        var end = DateTime.UtcNow;
        
        await Send.OkAsync(new TimeoutResponse
        {
            RequestedDelayMs = req.DelayMs,
            ActualDelayMs = (int)(end - start).TotalMilliseconds,
            Completed = true,
            StartTime = start,
            EndTime = end
        }, ct);
    }
}

[JsonSerializable(typeof(TimeoutRequest))]
[JsonSerializable(typeof(TimeoutResponse))]
public partial class TimeoutSerCtx : JsonSerializerContext;
