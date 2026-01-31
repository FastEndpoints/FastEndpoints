using FastEndpoints;
using System.Runtime.CompilerServices;

namespace NativeAotChecker.Endpoints;

// Request for async enumerable test
public class AsyncEnumerableRequest
{
    public int ItemCount { get; set; }
    public int DelayMs { get; set; }
}

public class AsyncEnumerableItem
{
    public int Index { get; set; }
    public string Value { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Tests IAsyncEnumerable streaming responses in AOT mode.
/// AOT ISSUE: IAsyncEnumerable state machine generation at runtime.
/// AsyncIteratorStateMachine attribute uses reflection.
/// Yield return compilation needs JIT for state machine.
/// </summary>
public class AsyncEnumerableEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("async-enumerable-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var countStr = Query<string>("count", false);
        var count = int.TryParse(countStr, out var c) ? c : 5;
        
        var items = new List<AsyncEnumerableItem>();
        await foreach (var item in GenerateItems(count, ct))
        {
            items.Add(item);
        }
        
        await Send.OkAsync(items);
    }

    private static async IAsyncEnumerable<AsyncEnumerableItem> GenerateItems(
        int count, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        for (int i = 0; i < count; i++)
        {
            if (ct.IsCancellationRequested) yield break;
            
            yield return new AsyncEnumerableItem
            {
                Index = i,
                Value = $"Item {i}",
                GeneratedAt = DateTime.UtcNow
            };
            
            await Task.Delay(10, ct);
        }
    }
}
