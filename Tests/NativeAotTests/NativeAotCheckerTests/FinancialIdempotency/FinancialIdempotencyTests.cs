using System.Net;
using System.Net.Http.Json;
using NativeAotChecker.Endpoints.FinancialIdempotency;

namespace NativeAotCheckerTests;

public class FinancialIdempotencyTests(App app) : TestBase<App>
{
    [Fact]
    public async Task Same_Key_And_Payload_Replays_Without_Executing_Again()
    {
        var key = Guid.NewGuid().ToString();

        using var first = await Post(key, 10);
        first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync(Cancellation));
        var firstRes = await Read(first);

        using var replay = await Post(key, 10);
        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        replay.Headers.GetValues("Idempotency-Key").ShouldContain(key);
        var replayRes = await Read(replay);

        replayRes.Amount.ShouldBe(10);
        replayRes.Execution.ShouldBe(firstRes.Execution);
    }

    [Fact]
    public async Task Same_Key_With_Different_Payload_Is_Rejected_Without_Executing()
    {
        var key = Guid.NewGuid().ToString();

        using var first = await Post(key, 20);
        first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync(Cancellation));
        var firstRes = await Read(first);

        using var conflict = await Post(key, 21);
        conflict.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var error = await conflict.Content.ReadFromJsonAsync<ErrorResponse>(new Config().Serializer.Options, Cancellation);
        error!.StatusCode.ShouldBe(422);

        using var next = await Post(Guid.NewGuid().ToString(), 22);
        (await Read(next)).Execution.ShouldBe(firstRes.Execution + 1);
    }

    Task<HttpResponseMessage> Post(string key, int amount)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "financial-charge")
        {
            Content = JsonContent.Create(new FinancialChargeRequest { Amount = amount }, options: new Config().Serializer.Options)
        };
        req.Headers.Add("Idempotency-Key", key);

        return app.Client.SendAsync(req, Cancellation);
    }

    async Task<FinancialChargeResponse> Read(HttpResponseMessage rsp)
        => (await rsp.Content.ReadFromJsonAsync<FinancialChargeResponse>(new Config().Serializer.Options, Cancellation))!;
}
