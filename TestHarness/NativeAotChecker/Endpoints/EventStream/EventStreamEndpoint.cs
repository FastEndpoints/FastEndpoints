using System.Runtime.CompilerServices;

namespace NativeAotChecker.Endpoints.EventStream;

public sealed class StockPrice
{
    public string Symbol { get; set; }
    public decimal LastPrice { get; set; }
}

public sealed class EventStreamEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("event-stream");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.EventStreamAsync("price-update", GetPrices(ct), ct);

    static async IAsyncEnumerable<StockPrice> GetPrices([EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 1; i <= 3; i++)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();

            yield return new() { Symbol = $"SYM{i}", LastPrice = i * 10.5m };
        }
    }
}
