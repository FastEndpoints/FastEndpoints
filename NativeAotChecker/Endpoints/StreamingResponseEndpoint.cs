namespace NativeAotChecker.Endpoints;

// Test IAsyncEnumerable streaming response - likely AOT issue
public sealed class StreamingItem
{
    public int Index { get; set; }
    public string Data { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

public sealed class StreamingResponseEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("streaming-response");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(GenerateItems(ct));
    }

    private static async IAsyncEnumerable<StreamingItem> GenerateItems([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < 5; i++)
        {
            if (ct.IsCancellationRequested) yield break;
            
            yield return new StreamingItem
            {
                Index = i,
                Data = $"Item {i}",
                Timestamp = DateTime.UtcNow
            };
            
            await Task.Delay(10, ct);
        }
    }
}
