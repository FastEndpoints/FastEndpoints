using System.Net;
using System.Net.Http.Json;
using System.Text;
using TestCases.FinancialIdempotency;

namespace FinancialIdempotencyTests;

public class FinancialIdempotencyTests(Sut App) : TestBase<Sut>
{
    const string ChargeUrl = "/api/test-cases/financial-idempotency/charge";
    const string FormUrl = "/api/test-cases/financial-idempotency/form";
    const string OversizeUrl = "/api/test-cases/financial-idempotency/oversize";
    const string ThrowUrl = "/api/test-cases/financial-idempotency/throw";
    const string InvalidUrl = "/api/test-cases/financial-idempotency/invalid";
    const string ProcessorsUrl = "/api/test-cases/financial-idempotency/processors";
    const string CasedUrl = "/api/test-cases/financial-idempotency/cased";
    const string StreamUrl = "/api/test-cases/financial-idempotency/stream";
    const string ConcurrentUrl = "/api/test-cases/financial-idempotency/concurrent";
    const string RawUrl = "/api/test-cases/financial-idempotency/raw";

    [Fact]
    public async Task Missing_Header_Is_400()
    {
        var res = await App.GuestClient.SendAsync(new(HttpMethod.Post, ChargeUrl) { Content = JsonBody(10) }, Cancellation);
        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Error(res)).ShouldContain("Idempotency header");
    }

    [Fact]
    public async Task Duplicate_Header_Is_400()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, ChargeUrl) { Content = JsonBody(10) };
        req.Headers.Add("Idempotency-Key", ["1", "2"]);
        var res = await App.GuestClient.SendAsync(req, Cancellation);
        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Error(res)).ShouldContain("Multiple idempotency headers");
    }

    [Fact]
    public async Task Same_Payload_Replays_And_Maps_Status()
    {
        var key = Guid.NewGuid().ToString();
        var handles = ChargeEndpoint.HandleCount;

        var res1 = await Post(ChargeUrl, key, JsonBody(10));
        res1.StatusCode.ShouldBe(HttpStatusCode.Created);
        res1.Headers.GetValues("Idempotency-Key").ShouldContain(key);
        var body1 = await res1.Content.ReadFromJsonAsync<ChargeResponse>(Cancellation);
        body1!.Ticks.ShouldBeGreaterThan(0);

        var res2 = await Post(ChargeUrl, key, JsonBody(10));
        res2.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body2 = await res2.Content.ReadFromJsonAsync<ChargeResponse>(Cancellation);
        body2!.Ticks.ShouldBe(body1.Ticks);
        ChargeEndpoint.HandleCount.ShouldBe(handles + 1);
    }

    [Fact]
    public async Task Different_Payload_Is_409()
    {
        var key = Guid.NewGuid().ToString();
        var handles = ChargeEndpoint.HandleCount;

        (await Post(ChargeUrl, key, JsonBody(10))).StatusCode.ShouldBe(HttpStatusCode.Created);

        var res = await Post(ChargeUrl, key, JsonBody(11));
        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Error(res)).ShouldBe(FinancialIdempotencyMiddleware.ConflictMessage);
        ChargeEndpoint.HandleCount.ShouldBe(handles + 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Trailing_Slash_Variants_Replay_And_Conflict(bool slashFirst)
    {
        var key = Guid.NewGuid().ToString();
        var handles = ChargeEndpoint.HandleCount;
        var firstUrl = slashFirst ? ChargeUrl + "/" : ChargeUrl;
        var retryUrl = slashFirst ? ChargeUrl : ChargeUrl + "/";

        var first = await Post(firstUrl, key, JsonBody(10));
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await first.Content.ReadFromJsonAsync<ChargeResponse>(Cancellation);

        var replay = await Post(retryUrl, key, JsonBody(10));
        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await replay.Content.ReadFromJsonAsync<ChargeResponse>(Cancellation))!.Ticks.ShouldBe(body!.Ticks);

        var conflict = await Post(retryUrl, key, JsonBody(11));
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Error(conflict)).ShouldBe(FinancialIdempotencyMiddleware.ConflictMessage);
        ChargeEndpoint.HandleCount.ShouldBe(handles + 1);
    }

    [Fact]
    public async Task User_Agent_Change_Still_Replays()
    {
        var key = Guid.NewGuid().ToString();
        var handles = ChargeEndpoint.HandleCount;
        var ticks = (await (await Post(ChargeUrl, key, JsonBody(10), extra: r => r.Headers.TryAddWithoutValidation("User-Agent", "ua-1")))
            .Content.ReadFromJsonAsync<ChargeResponse>(Cancellation))!.Ticks;

        var res = await Post(ChargeUrl, key, JsonBody(10), extra: r => r.Headers.TryAddWithoutValidation("User-Agent", "ua-2"));
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await res.Content.ReadFromJsonAsync<ChargeResponse>(Cancellation))!.Ticks.ShouldBe(ticks);
        ChargeEndpoint.HandleCount.ShouldBe(handles + 1);
    }

    [Fact]
    public async Task Authorization_Change_Still_Replays()
    {
        var key = Guid.NewGuid().ToString();
        var handles = ChargeEndpoint.HandleCount;

        (await Post(ChargeUrl, key, JsonBody(10), extra: r => r.Headers.TryAddWithoutValidation("Authorization", "Bearer one")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var res = await Post(ChargeUrl, key, JsonBody(10), extra: r => r.Headers.TryAddWithoutValidation("Authorization", "Bearer two"));
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        ChargeEndpoint.HandleCount.ShouldBe(handles + 1);
    }

    [Fact]
    public async Task Cookie_Change_Still_Replays()
    {
        var key = Guid.NewGuid().ToString();
        var handles = ChargeEndpoint.HandleCount;

        (await Post(ChargeUrl, key, JsonBody(10), extra: r => r.Headers.TryAddWithoutValidation("Cookie", "a=1")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        (await Post(ChargeUrl, key, JsonBody(10), extra: r => r.Headers.TryAddWithoutValidation("Cookie", "a=2")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        ChargeEndpoint.HandleCount.ShouldBe(handles + 1);
    }

    [Fact]
    public async Task Path_Casing_Is_Insensitive_By_Default()
    {
        var key = Guid.NewGuid().ToString();
        var handles = ChargeEndpoint.HandleCount;
        var ticks = (await (await Post(ChargeUrl, key, JsonBody(10))).Content.ReadFromJsonAsync<ChargeResponse>(Cancellation))!.Ticks;

        var res = await Post("/api/test-cases/financial-idempotency/Charge", key, JsonBody(10));
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await res.Content.ReadFromJsonAsync<ChargeResponse>(Cancellation))!.Ticks.ShouldBe(ticks);
        ChargeEndpoint.HandleCount.ShouldBe(handles + 1);
    }

    [Fact]
    public async Task UseCaseSensitivePaths_Is_Distinct_Identity()
    {
        var key = Guid.NewGuid().ToString();
        var handles = CaseSensitiveEndpoint.HandleCount;

        (await Post(CasedUrl, key, JsonBody(10))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await Post("/api/test-cases/financial-idempotency/Cased", key, JsonBody(10))).StatusCode.ShouldBe(HttpStatusCode.Created);
        CaseSensitiveEndpoint.HandleCount.ShouldBe(handles + 2);
    }

    [Fact]
    public async Task Host_Casing_Is_Insensitive()
    {
        var key = Guid.NewGuid().ToString();
        var handles = ChargeEndpoint.HandleCount;
        var ticks = (await (await Post(ChargeUrl, key, JsonBody(10), extra: r => r.Headers.Host = "localhost"))
            .Content.ReadFromJsonAsync<ChargeResponse>(Cancellation))!.Ticks;

        var res = await Post(ChargeUrl, key, JsonBody(10), extra: r => r.Headers.Host = "LOCALHOST");
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await res.Content.ReadFromJsonAsync<ChargeResponse>(Cancellation))!.Ticks.ShouldBe(ticks);
        ChargeEndpoint.HandleCount.ShouldBe(handles + 1);
    }

    [Fact]
    public async Task Query_Change_Is_409_And_Order_Does_Not_Matter()
    {
        var key = Guid.NewGuid().ToString();
        var handles = ChargeEndpoint.HandleCount;
        var ticks = (await (await Post(ChargeUrl + "?b=2&a=1", key, JsonBody(10))).Content.ReadFromJsonAsync<ChargeResponse>(Cancellation))!.Ticks;

        var replay = await Post(ChargeUrl + "?a=1&b=2", key, JsonBody(10));
        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await replay.Content.ReadFromJsonAsync<ChargeResponse>(Cancellation))!.Ticks.ShouldBe(ticks);

        var conflict = await Post(ChargeUrl + "?a=1&b=3", key, JsonBody(10));
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        ChargeEndpoint.HandleCount.ShouldBe(handles + 1);
    }

    [Fact]
    public async Task Query_And_Body_Concat_Does_Not_Collide()
    {
        var key = Guid.NewGuid().ToString();
        (await Post(RawUrl + "?x=1", key, RawJson("00"))).StatusCode.ShouldBe(HttpStatusCode.Created);

        var res = await Post(RawUrl + "?x=10", key, RawJson("0"));
        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Form_Concat_And_Multi_Value_Do_Not_Collide_Field_Order_Does()
    {
        var key1 = Guid.NewGuid().ToString();
        (await Post(FormUrl, key1, Form(("Amount", "1"), ("a", "bc")))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await Post(FormUrl, key1, Form(("Amount", "1"), ("ab", "c")))).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var key2 = Guid.NewGuid().ToString();
        var handles = FormEndpoint.HandleCount;
        var ticks = (await (await Post(FormUrl, key2, Form(("Amount", "1"), ("a", "1"), ("b", "2"))))
            .Content.ReadFromJsonAsync<ChargeResponse>(Cancellation))!.Ticks;
        var replay = await Post(FormUrl, key2, Form(("Amount", "1"), ("b", "2"), ("a", "1")));
        replay.StatusCode.ShouldBe(HttpStatusCode.Created); // first was 201; replay keeps 201 (no ReplayStatusCode)
        (await replay.Content.ReadFromJsonAsync<ChargeResponse>(Cancellation))!.Ticks.ShouldBe(ticks);
        FormEndpoint.HandleCount.ShouldBe(handles + 1);

        var key3 = Guid.NewGuid().ToString();
        (await Post(FormUrl, key3, Form(("Amount", "1"), ("a", "1,2")))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await Post(FormUrl, key3, Form(("Amount", "1"), ("a", "1"), ("a", "2")))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Concurrent_Same_Payload_Executes_Once()
    {
        var key = Guid.NewGuid().ToString();
        var handles = ConcurrentEndpoint.HandleCount;
        ConcurrentEndpoint.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ConcurrentEndpoint.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            var first = Post(ConcurrentUrl, key, JsonBody(10));
            await ConcurrentEndpoint.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var second = await Post(ConcurrentUrl, key, JsonBody(10));
            second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            ConcurrentEndpoint.Release.TrySetResult();
            (await first).StatusCode.ShouldBe(HttpStatusCode.Created);
            ConcurrentEndpoint.HandleCount.ShouldBe(handles + 1);
        }
        finally
        {
            ConcurrentEndpoint.Release?.TrySetResult();
            ConcurrentEndpoint.Entered = null;
            ConcurrentEndpoint.Release = null;
        }
    }

    [Fact]
    public async Task Concurrent_Different_Payloads_One_409()
    {
        var key = Guid.NewGuid().ToString();
        var handles = ConcurrentEndpoint.HandleCount;
        ConcurrentEndpoint.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ConcurrentEndpoint.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            var first = Post(ConcurrentUrl, key, JsonBody(10));
            await ConcurrentEndpoint.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var second = Post(ConcurrentUrl, key, JsonBody(11));
            var conflict = await second;
            conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            ConcurrentEndpoint.Release.TrySetResult();
            (await first).StatusCode.ShouldBe(HttpStatusCode.Created);
            ConcurrentEndpoint.HandleCount.ShouldBe(handles + 1);
        }
        finally
        {
            ConcurrentEndpoint.Release?.TrySetResult();
            ConcurrentEndpoint.Entered = null;
            ConcurrentEndpoint.Release = null;
        }
    }

    [Fact]
    public async Task Invalid_First_Request_Retains_The_Key()
    {
        var key = Guid.NewGuid().ToString();
        var handles = InvalidEndpoint.HandleCount;

        (await Post(InvalidUrl, key, JsonBody(0))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        InvalidEndpoint.HandleCount.ShouldBe(handles);

        var res = await Post(InvalidUrl, key, JsonBody(5));
        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        InvalidEndpoint.HandleCount.ShouldBe(handles);
    }

    [Fact]
    public async Task Thrown_Handler_Retains_The_Key()
    {
        var key = Guid.NewGuid().ToString();
        var handles = ThrowEndpoint.HandleCount;

        (await Post(ThrowUrl, key, JsonBody(-1))).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);

        var res = await Post(ThrowUrl, key, JsonBody(5));
        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        ThrowEndpoint.HandleCount.ShouldBe(handles + 1);
    }

    [Fact]
    public async Task Oversize_Body_Fails_Closed()
    {
        var key = Guid.NewGuid().ToString();
        var handles = OversizeEndpoint.HandleCount;

        var first = await Post(OversizeUrl, key);
        first.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        OversizeEndpoint.HandleCount.ShouldBe(handles + 1);

        var retry = await Post(OversizeUrl, key);
        retry.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await Error(retry)).ShouldBe(FinancialIdempotencyMiddleware.UnreplayableMessage);
        OversizeEndpoint.HandleCount.ShouldBe(handles + 1);

        var other = await Post(OversizeUrl + "?n=2", key);
        other.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        OversizeEndpoint.HandleCount.ShouldBe(handles + 1);
    }

    [Fact]
    public async Task Streamed_2xx_Is_Captured()
    {
        var key = Guid.NewGuid().ToString();
        var handles = StreamEndpoint.HandleCount;

        var first = await Post(StreamUrl, key);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await first.Content.ReadAsStringAsync(Cancellation)).ShouldBe("streamed");

        var replay = await Post(StreamUrl, key);
        replay.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await replay.Content.ReadAsStringAsync(Cancellation)).ShouldBe("streamed");
        StreamEndpoint.HandleCount.ShouldBe(handles + 1);
    }

    [Fact]
    public async Task Processors_Do_Not_Run_On_Replay_Or_Conflict()
    {
        var key = Guid.NewGuid().ToString();
        var handles = ProcessorEndpoint.HandleCount;
        var pre = ProcessorEndpoint.PreCount;
        var post = ProcessorEndpoint.PostCount;

        (await Post(ProcessorsUrl, key, JsonBody(10))).StatusCode.ShouldBe(HttpStatusCode.Created);
        ProcessorEndpoint.HandleCount.ShouldBe(handles + 1);
        ProcessorEndpoint.PreCount.ShouldBe(pre + 1);
        ProcessorEndpoint.PostCount.ShouldBe(post + 1);

        (await Post(ProcessorsUrl, key, JsonBody(10))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await Post(ProcessorsUrl, key, JsonBody(11))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        ProcessorEndpoint.HandleCount.ShouldBe(handles + 1);
        ProcessorEndpoint.PreCount.ShouldBe(pre + 1);
        ProcessorEndpoint.PostCount.ShouldBe(post + 1);
    }

    [Fact]
    public async Task Fingerprint_Idempotency_Still_Works()
    {
        var key = Guid.NewGuid().ToString();
        var client = App.CreateClient(c => c.DefaultRequestHeaders.Add("Idempotency-Key", key));
        var req = new TestCases.Idempotency.Request { Content = "hello" };

        var (res1, rsp1) = await client.GETAsync<TestCases.Idempotency.Endpoint, TestCases.Idempotency.Request, TestCases.Idempotency.Response>(req);
        res1.IsSuccessStatusCode.ShouldBeTrue();
        var ticks = rsp1.Ticks;

        var (res2, rsp2) = await client.GETAsync<TestCases.Idempotency.Endpoint, TestCases.Idempotency.Request, TestCases.Idempotency.Response>(req);
        res2.IsSuccessStatusCode.ShouldBeTrue();
        rsp2.Ticks.ShouldBe(ticks);
    }

    async Task<HttpResponseMessage> Post(string url, string key, HttpContent? content = null, Action<HttpRequestMessage>? extra = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        req.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        extra?.Invoke(req);

        return await App.GuestClient.SendAsync(req, Cancellation);
    }

    static JsonContent JsonBody(int amount)
        => JsonContent.Create(new ChargeRequest { Amount = amount });

    static StringContent RawJson(string body)
        => new(body, Encoding.UTF8, "application/json");

    static FormUrlEncodedContent Form(params (string Key, string Value)[] fields)
        => new(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));

    static async Task<string> Error(HttpResponseMessage res)
    {
        var body = await res.Content.ReadFromJsonAsync<ErrorResponse>();
        body.ShouldNotBeNull();

        return body.Errors["generalErrors"][0];
    }
}
