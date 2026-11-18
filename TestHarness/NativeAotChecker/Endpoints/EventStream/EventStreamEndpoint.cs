using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints.EventStream;

public sealed class StockPrice
{
    public string Symbol { get; set; }
    public decimal LastPrice { get; set; }
}

// the serializer context generator doesn't detect sse data types, so they must be registered manually.
[JsonSerializable(typeof(StockPrice))]
internal partial class EventStreamSerializerContext : JsonSerializerContext;

public sealed class EventStreamEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("event-stream");
        AllowAnonymous();
        SerializerContext<EventStreamSerializerContext>();
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
