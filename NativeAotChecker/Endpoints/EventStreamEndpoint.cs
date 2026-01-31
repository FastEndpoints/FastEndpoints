using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Server-Sent Events (SSE) / EventStream in AOT mode
public sealed class EventStreamRequest
{
    [QueryParam]
    public string EventName { get; set; } = "message";

    [QueryParam]
    public int EventCount { get; set; } = 5;

    [QueryParam]
    public int DelayMs { get; set; } = 100;
}

public sealed class EventStreamData
{
    public int Index { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public sealed class EventStreamEndpoint : Endpoint<EventStreamRequest>
{
    public override void Configure()
    {
        Get("event-stream-test");
        AllowAnonymous();
        SerializerContext<EventStreamSerCtx>();
    }

    public override async Task HandleAsync(EventStreamRequest req, CancellationToken ct)
    {
        await Send.EventStreamAsync(req.EventName, GenerateEvents(req, ct), ct);
    }

    private static async IAsyncEnumerable<EventStreamData> GenerateEvents(
        EventStreamRequest req, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < req.EventCount && !ct.IsCancellationRequested; i++)
        {
            yield return new EventStreamData
            {
                Index = i,
                Message = $"Event {i + 1} of {req.EventCount}",
                Timestamp = DateTime.UtcNow
            };

            if (req.DelayMs > 0 && i < req.EventCount - 1)
            {
                await Task.Delay(req.DelayMs, ct);
            }
        }
    }
}

[JsonSerializable(typeof(EventStreamRequest))]
[JsonSerializable(typeof(EventStreamData))]
[JsonSerializable(typeof(EventStreamData[]))]
public partial class EventStreamSerCtx : JsonSerializerContext;
