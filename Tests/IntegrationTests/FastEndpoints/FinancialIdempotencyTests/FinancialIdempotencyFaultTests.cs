using System.Net;
using System.Net.Http.Json;
using TestCases.FinancialIdempotency;

namespace FinancialIdempotencyTests;

public class FinancialIdempotencyFaultSut : AppFixture<Web.Program>
{
    public FaultyFinancialIdempotencyStore Store { get; } = new();

    protected override void ConfigureServices(IServiceCollection s)
        => s.AddSingleton<IFinancialIdempotencyStore>(Store);
}

public class FinancialIdempotencyFaultTests(FinancialIdempotencyFaultSut App) : TestBase<FinancialIdempotencyFaultSut>
{
    const string ChargeUrl = "/api/test-cases/financial-idempotency/charge";

    [Fact]
    public async Task In_Flight_From_Distributed_Store_Is_409()
    {
        App.Store.Reset();
        App.Store.AlwaysInFlight = true;
        var res = await Post(ChargeUrl, Guid.NewGuid().ToString(), JsonBody(10));
        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Error(res)).ShouldBe(FinancialIdempotencyMiddleware.InProgressMessage);
    }

    [Fact]
    public async Task Complete_Write_Failure_Fails_Closed()
    {
        App.Store.Reset();
        App.Store.ThrowOnComplete = true;
        var key = Guid.NewGuid().ToString();

        var first = await Post(ChargeUrl, key, JsonBody(10));
        first.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);

        var retry = await Post(ChargeUrl, key, JsonBody(10));
        retry.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await Error(retry)).ShouldBe(FinancialIdempotencyMiddleware.UnreplayableMessage);

        var other = await Post(ChargeUrl, key, JsonBody(11));
        other.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Lost_Completion_Acknowledgement_Does_Not_Destroy_Replay()
    {
        App.Store.Reset();
        App.Store.ThrowAfterComplete = true;
        var key = Guid.NewGuid().ToString();
        (await Post(ChargeUrl, key, JsonBody(10))).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var retry = await Post(ChargeUrl, key, JsonBody(10));
        retry.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await retry.Content.ReadFromJsonAsync<ChargeResponse>())!.Amount.ShouldBe(10);
    }

    async Task<HttpResponseMessage> Post(string url, string key, HttpContent content)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        req.Headers.TryAddWithoutValidation("Idempotency-Key", key);

        return await App.Client.SendAsync(req, Cancellation);
    }

    static JsonContent JsonBody(int amount)
        => JsonContent.Create(new ChargeRequest { Amount = amount });

    static async Task<string> Error(HttpResponseMessage res)
    {
        var body = await res.Content.ReadFromJsonAsync<ErrorResponse>();
        body.ShouldNotBeNull();

        return body.Errors["generalErrors"][0];
    }
}

public sealed class FaultyFinancialIdempotencyStore : IFinancialIdempotencyStore
{
    readonly MemoryFinancialIdempotencyStore _inner = new();

    public bool AlwaysInFlight { get; set; }
    public bool ThrowOnComplete { get; set; }
    public bool ThrowAfterComplete { get; set; }

    public void Reset()
    {
        AlwaysInFlight = false;
        ThrowOnComplete = false;
        ThrowAfterComplete = false;
    }

    public ValueTask<FinancialBeginResult> TryBeginAsync(string identityKey,
                                                         ReadOnlyMemory<byte> payloadHash,
                                                         TimeSpan ttl,
                                                         CancellationToken ct)
        => AlwaysInFlight
               ? new(FinancialBeginResult.InFlight())
               : _inner.TryBeginAsync(identityKey, payloadHash, ttl, ct);

    public async Task<FinancialSettlementResult> CompleteAsync(string identityKey,
                                   string reservationToken,
                                   FinancialIdempotencyResponse response,
                                   TimeSpan ttl,
                                   CancellationToken ct)
    {
        if (ThrowOnComplete)
            throw new IOException("complete failed");

        var result = await _inner.CompleteAsync(identityKey, reservationToken, response, ttl, ct);
        if (ThrowAfterComplete)
            throw new IOException("acknowledgement lost");
        return result;
    }

    public Task<FinancialSettlementResult> MarkUnreplayableAsync(string identityKey,
                                           string reservationToken,
                                           CancellationToken ct)
        => _inner.MarkUnreplayableAsync(identityKey, reservationToken, ct);

    public Task<FinancialSettlementResult> AbandonAsync(string identityKey, string reservationToken, CancellationToken ct)
        => _inner.AbandonAsync(identityKey, reservationToken, ct);
}
