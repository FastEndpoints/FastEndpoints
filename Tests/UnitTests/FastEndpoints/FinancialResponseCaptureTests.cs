using System.Net;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Unit.FastEndpoints;

public class FinancialResponseCaptureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Kestrel_Finalizes_Pending_Pipe_Bytes_And_OnStarting(bool empty)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        var callbacks = new List<int>();
        app.Run(async ctx =>
        {
            var originalResponse = ctx.Features.Get<IHttpResponseFeature>()!;
            var originalBody = ctx.Features.Get<IHttpResponseBodyFeature>()!;
            await using var capture = new FinancialResponseCapture(originalResponse, 1024);
            ctx.Features.Set<IHttpResponseFeature>(capture);
            ctx.Features.Set<IHttpResponseBodyFeature>(capture);
            byte[] bytes;
            try
            {
                ctx.Response.OnStarting(() =>
                {
                    callbacks.Add(1);
                    ctx.Response.StatusCode = empty ? 204 : 201;
                    ctx.Response.Headers["X-Final"] = "yes";
                    return Task.CompletedTask;
                });
                ctx.Response.OnStarting(() => { callbacks.Add(2); return Task.CompletedTask; });
                if (!empty)
                {
                    "hello"u8.CopyTo(ctx.Response.BodyWriter.GetSpan(5));
                    ctx.Response.BodyWriter.Advance(5);
                }
                bytes = await capture.SnapshotAsync();
                await capture.CompleteAsync();
            }
            finally
            {
                ctx.Features.Set(originalResponse);
                ctx.Features.Set(originalBody);
            }
            ctx.Response.StatusCode = capture.StatusCode;
            ctx.Response.Headers["X-Final"] = capture.Headers["X-Final"];
            if (!empty)
                await ctx.Response.Body.WriteAsync(bytes);
        });
        await app.StartAsync();
        using var client = new HttpClient();
        var response = await client.GetAsync(app.Urls.Single());
        response.StatusCode.ShouldBe(empty ? HttpStatusCode.NoContent : HttpStatusCode.Created);
        (await response.Content.ReadAsStringAsync()).ShouldBe(empty ? "" : "hello");
        response.Headers.GetValues("X-Final").Single().ShouldBe("yes");
        callbacks.ShouldBe([2, 1]);
    }

    [Theory]
    [InlineData("partial", 500)]
    [InlineData("empty", 204)]
    [InlineData("reject", 400)]
    [InlineData("ambiguous", 400)]
    [InlineData("compressed", 200)]
    public async Task Kestrel_Middleware_Retains_Ambiguity_And_Releases_Only_Safe_Rejection(string mode, int firstStatus)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddResponseCompression();
        await using var app = builder.Build();
        var store = new MemoryFinancialIdempotencyStore();
        var definition = new EndpointDefinition(typeof(FinancialResponseCaptureTests), typeof(object), typeof(object));
        definition.FinancialIdempotency(o => o.CallerScope = _ => "account");
        var executions = 0;
        app.Use(async (ctx, next) =>
        {
            try { await next(ctx); }
            catch { ctx.Response.Clear(); ctx.Response.StatusCode = 500; }
        });
        app.UseResponseCompression();
        app.Use(async (ctx, next) =>
        {
            ctx.SetEndpoint(new Microsoft.AspNetCore.Http.Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(definition), "financial"));
            await new FinancialIdempotencyMiddleware(next, store, Microsoft.Extensions.Logging.Abstractions.NullLogger<FinancialIdempotencyMiddleware>.Instance).Invoke(ctx);
        });
        app.Run(async ctx =>
        {
            executions++;
            if (mode == "partial")
            {
                await ctx.Response.WriteAsync("partial");
                throw new InvalidOperationException("after side effect");
            }
            ctx.Response.StatusCode = firstStatus;
            if (mode == "compressed")
            {
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.WriteAsync("compressible response");
            }
            if (mode == "reject")
                ctx.RejectFinancialIdempotencyWithoutSideEffects();
        });
        await app.StartAsync();
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", "same");
        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip");
        var first = await client.PostAsync(app.Urls.Single(), new StringContent("same"));
        ((int)first.StatusCode).ShouldBe(firstStatus);
        var retry = await client.PostAsync(app.Urls.Single(), new StringContent("same"));
        ((int)retry.StatusCode).ShouldBe(mode is "partial" or "ambiguous" ? 500 : firstStatus);
        executions.ShouldBe(mode == "reject" ? 2 : 1);
        if (mode == "compressed")
        {
            first.Content.Headers.ContentEncoding.ShouldContain("gzip");
            retry.Content.Headers.ContentEncoding.ShouldContain("gzip");
            (await retry.Content.ReadAsByteArrayAsync()).ShouldBe(await first.Content.ReadAsByteArrayAsync());
        }
        if (mode == "partial")
            (await retry.Content.ReadAsStringAsync()).ShouldNotContain("partial");
    }

    [Theory]
    [InlineData("POST", 201, null, true, true)]
    [InlineData("HEAD", 200, null, false, false)]
    [InlineData("POST", 204, null, false, false)]
    [InlineData("POST", 205, null, false, false)]
    [InlineData("POST", 201, 204, true, false)]
    [InlineData("POST", 201, 205, true, false)]
    [InlineData("POST", 304, null, false, false)]
    public async Task Middleware_Delivers_Exact_Body_Or_Clears_Content_Length(
        string method, int status, int? replayStatus, bool firstBodyAllowed, bool replayBodyAllowed)
    {
        var store = new MemoryFinancialIdempotencyStore();
        var definition = new EndpointDefinition(typeof(FinancialResponseCaptureTests), typeof(object), typeof(object));
        definition.FinancialIdempotency(o =>
        {
            o.CallerScope = _ => "account";
            o.ReplayStatusCode = replayStatus;
        });
        var body = System.Text.Encoding.UTF8.GetBytes("response \u00e9");
        var executions = 0;
        var middleware = new FinancialIdempotencyMiddleware(async ctx =>
        {
            executions++;
            ctx.Response.StatusCode = status;
            ctx.Response.ContentLength = 999;
            await ctx.Response.Body.WriteAsync(body);
        }, store, Microsoft.Extensions.Logging.Abstractions.NullLogger<FinancialIdempotencyMiddleware>.Instance);

        var first = CreateContext();
        await middleware.Invoke(first);
        AssertDelivery(first, status, firstBodyAllowed);

        if (status == 304)
        {
            var opts = definition.FinancialIdempotencyOptions!;
            var identity = FinancialIdentity.Build(first.Request, opts, "account");
            var hash = await FinancialPayloadHash.ComputeAsync(first.Request, CancellationToken.None);
            (await store.TryBeginAsync(identity, hash, opts.Duration, CancellationToken.None)).Kind.ShouldBe(FinancialBeginKind.Unreplayable);
        }
        else
        {
            var replay = CreateContext();
            await middleware.Invoke(replay);
            AssertDelivery(replay, replayStatus ?? status, replayBodyAllowed);
        }
        executions.ShouldBe(1);

        DefaultHttpContext CreateContext()
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = method;
            ctx.Request.Headers["Idempotency-Key"] = "same";
            ctx.Response.Body = new MemoryStream();
            ctx.Response.ContentLength = 999;
            ctx.SetEndpoint(new Microsoft.AspNetCore.Http.Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(definition), "financial"));

            return ctx;
        }

        void AssertDelivery(DefaultHttpContext ctx, int expectedStatus, bool bodyAllowed)
        {
            ctx.Response.StatusCode.ShouldBe(expectedStatus);
            ctx.Response.ContentLength.ShouldBe(bodyAllowed ? (long?)body.Length : null);
            ((MemoryStream)ctx.Response.Body).ToArray().ShouldBe(bodyAllowed ? body : []);
        }
    }

    [Fact]
    public async Task Middleware_Filters_Stored_Headers_Without_Filtering_First_Response()
    {
        var store = new MemoryFinancialIdempotencyStore();
        var definition = new EndpointDefinition(typeof(FinancialResponseCaptureTests), typeof(object), typeof(object));
        definition.FinancialIdempotency(o => o.CallerScope = _ => "account");
        var body = System.Text.Encoding.UTF8.GetBytes("response");
        var executions = 0;
        var middleware = new FinancialIdempotencyMiddleware(async ctx =>
        {
            executions++;
            ctx.Response.StatusCode = 201;
            ctx.Response.Headers["X-Retained"] = new Microsoft.Extensions.Primitives.StringValues(["first", "second"]);
            ctx.Response.Headers["sEt-CoOkIe"] = "session=value";
            ctx.Response.Headers["kEeP-aLiVe"] = "timeout=5";
            ctx.Response.Headers["Connection"] = " , x-NoMiNaTeD, ";
            ctx.Response.Headers["X-Nominated"] = "connection-only";
            ctx.Response.ContentLength = 999;
            await ctx.Response.Body.WriteAsync(body);
        }, store, Microsoft.Extensions.Logging.Abstractions.NullLogger<FinancialIdempotencyMiddleware>.Instance);

        var first = CreateContext();
        await middleware.Invoke(first);
        first.Response.Headers["X-Retained"].ToArray().ShouldBe(["first", "second"]);
        first.Response.Headers["Set-Cookie"].ToString().ShouldBe("session=value");
        first.Response.Headers["Keep-Alive"].ToString().ShouldBe("timeout=5");
        first.Response.Headers["Connection"].ToString().ShouldBe(" , x-NoMiNaTeD, ");
        first.Response.Headers["X-Nominated"].ToString().ShouldBe("connection-only");
        first.Response.ContentLength.ShouldBe(body.LongLength);
        ((MemoryStream)first.Response.Body).ToArray().ShouldBe(body);

        var opts = definition.FinancialIdempotencyOptions!;
        var identity = FinancialIdentity.Build(first.Request, opts, "account");
        var hash = await FinancialPayloadHash.ComputeAsync(first.Request, CancellationToken.None);
        var stored = await store.TryBeginAsync(identity, hash, opts.Duration, CancellationToken.None);
        stored.Kind.ShouldBe(FinancialBeginKind.Replay);
        stored.Response!.Headers.Select(h => h.Key).ShouldBe(["Idempotency-Key", "X-Retained"]);
        stored.Response.Headers.Single(h => h.Key == "X-Retained").Value.ShouldBe(["first", "second"]);

        var replay = CreateContext();
        await middleware.Invoke(replay);
        replay.Response.StatusCode.ShouldBe(201);
        replay.Response.Headers["X-Retained"].ToArray().ShouldBe(["first", "second"]);
        foreach (var name in new[] { "Set-Cookie", "Keep-Alive", "Connection", "X-Nominated" })
            replay.Response.Headers.ContainsKey(name).ShouldBeFalse();
        replay.Response.ContentLength.ShouldBe(body.LongLength);
        ((MemoryStream)replay.Response.Body).ToArray().ShouldBe(body);
        executions.ShouldBe(1);

        DefaultHttpContext CreateContext()
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = "POST";
            ctx.Request.Headers["Idempotency-Key"] = "same";
            ctx.Response.Body = new MemoryStream();
            ctx.SetEndpoint(new Microsoft.AspNetCore.Http.Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(definition), "financial"));

            return ctx;
        }
    }

    [Fact]
    public async Task Capture_Rejects_Status_Changes_And_Callback_Registration_After_Start()
    {
        await using var capture = new FinancialResponseCapture(new HttpResponseFeature { StatusCode = 201 }, 1024);
        await capture.StartAsync();

        var statusError = Should.Throw<InvalidOperationException>(() => capture.StatusCode = 202);
        statusError.Message.ShouldBe("The response has already started.");
        capture.StatusCode.ShouldBe(201);

        var callbackExecuted = false;
        var callbackError = Should.Throw<InvalidOperationException>(() => capture.OnStarting(_ =>
        {
            callbackExecuted = true;
            return Task.CompletedTask;
        }, new object()));
        callbackError.Message.ShouldBe("The response has already started.");

        await capture.CompleteAsync();
        callbackExecuted.ShouldBeFalse();
        capture.StatusCode.ShouldBe(201);
    }

    [Fact]
    public async Task Capture_Rejects_Overflow_While_Writing()
    {
        await using var capture = new FinancialResponseCapture(new HttpResponseFeature(), 4);
        await Should.ThrowAsync<IOException>(async () => await capture.Stream.WriteAsync(new byte[5]));
    }
}
