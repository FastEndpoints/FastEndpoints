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
    [InlineData("success", 202, 202, false)]
    [InlineData("success", 202, 202, true)]
    [InlineData("error", 400, 500, false)]
    [InlineData("error", 400, 500, true)]
    [InlineData("exception", 500, 500, false)]
    [InlineData("exception", 500, 500, true)]
    [InlineData("cancel", 202, 202, false)]
    [InlineData("empty", 204, 204, false)]
    public async Task Kestrel_Upstream_Callbacks_Run_Before_Settlement(string mode, int firstStatus, int retryStatus, bool flush)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        var store = new MemoryFinancialIdempotencyStore();
        builder.Services.AddFinancialIdempotency(store);
        await using var app = builder.Build();
        var definition = new EndpointDefinition(typeof(FinancialResponseCaptureTests), typeof(object), typeof(object));
        definition.FinancialIdempotency(o => o.CallerScope = _ => "account");
        var opts = definition.FinancialIdempotencyOptions!;
        var callbacks = new List<int>();
        var executions = 0;
        string identity = null!;
        byte[] hash = null!;
        var requests = 0;
        app.Use(async (ctx, next) =>
        {
            try { await next(ctx); }
            catch { ctx.Response.Clear(); ctx.Response.StatusCode = 500; }
        });
        app.Use(async (ctx, next) =>
        {
            var attempt = requests++;
            var transport = ctx.Features.Get<IHttpResponseFeature>()!;
            ctx.Response.OnStarting(() =>
            {
                callbacks.Add(1);
                return Task.CompletedTask;
            });
            ctx.Response.OnStarting(async () =>
            {
                callbacks.Add(2);
                if (attempt > 0)
                    return;
                transport.HasStarted.ShouldBeFalse();
                (await store.TryBeginAsync(identity, hash, opts.Duration, CancellationToken.None)).Kind.ShouldBe(FinancialBeginKind.InFlight);
                if (mode == "exception")
                    throw new InvalidOperationException("callback failed");
                ctx.Response.StatusCode = mode == "error" ? 400 : mode == "empty" ? 204 : 202;
                ctx.Response.Headers["X-Final"] = "upstream";
                ctx.Response.Headers["Set-Cookie"] = "session=first";
                if (mode == "cancel")
                    ctx.RequestAborted = new CancellationToken(canceled: true);
            });
            ctx.SetEndpoint(new Microsoft.AspNetCore.Http.Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(definition), "financial"));
            identity = FinancialIdentity.Build(ctx.Request, opts, "account");
            hash = await FinancialPayloadHash.ComputeAsync(ctx.Request, CancellationToken.None);
            await next(ctx);
        });
        app.UseFinancialIdempotency();
        app.Run(ctx =>
        {
            executions++;
            ctx.Response.StatusCode = 201;
            ctx.Response.OnStarting(() =>
            {
                callbacks.Add(3);
                ctx.Response.Headers["X-Final"] = "downstream";
                return Task.CompletedTask;
            });
            if (flush)
                return ctx.Response.WriteAsync("result");
            if (mode != "empty")
            {
                "result"u8.CopyTo(ctx.Response.BodyWriter.GetSpan(6));
                ctx.Response.BodyWriter.Advance(6);
            }
            return Task.CompletedTask;
        });
        await app.StartAsync();
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", "same");
        using var first = await client.PostAsync(app.Urls.Single(), new StringContent("same"));
        // Cancellation prevents delivery after the result has been settled.
        ((int)first.StatusCode).ShouldBe(mode == "cancel" ? 500 : firstStatus);
        callbacks.ShouldBe(mode == "exception" ? [3, 2] : [3, 2, 1]);
        if (mode is not ("exception" or "cancel"))
        {
            first.Headers.GetValues("X-Final").Single().ShouldBe("upstream");
            first.Headers.GetValues("Set-Cookie").Single().ShouldBe("session=first");
            (await first.Content.ReadAsStringAsync()).ShouldBe(mode == "empty" ? "" : "result");
        }
        var stored = await store.TryBeginAsync(identity, hash, opts.Duration, CancellationToken.None);
        stored.Kind.ShouldBe(mode is "error" or "exception" ? FinancialBeginKind.Unreplayable : FinancialBeginKind.Replay);
        if (stored.Response is not null)
        {
            stored.Response.StatusCode.ShouldBe(firstStatus);
            stored.Response.Headers.Single(h => h.Key == "X-Final").Value.ShouldBe(["upstream"]);
            stored.Response.Headers.ShouldNotContain(h => h.Key == "Set-Cookie");
        }
        using var retry = await client.PostAsync(app.Urls.Single(), new StringContent("same"));
        ((int)retry.StatusCode).ShouldBe(retryStatus);
        executions.ShouldBe(1);
        callbacks.ShouldBe(mode == "exception" ? [3, 2, 2, 1] : [3, 2, 1, 2, 1]);
        if (retryStatus < 300)
        {
            retry.Headers.GetValues("X-Final").Single().ShouldBe("upstream");
            (await retry.Content.ReadAsStringAsync()).ShouldBe(mode == "empty" ? "" : "result");
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Kestrel_Initialized_Cookies_Reach_First_Response_And_Are_Excluded_From_Replay(bool policy, bool delete)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        var store = new MemoryFinancialIdempotencyStore();
        builder.Services.AddFinancialIdempotency(store);
        await using var app = builder.Build();
        var definition = new EndpointDefinition(typeof(FinancialResponseCaptureTests), typeof(object), typeof(object));
        definition.FinancialIdempotency(o => o.CallerScope = _ => "account");
        var opts = definition.FinancialIdempotencyOptions!;
        var executions = 0;
        string identity = null!;
        byte[] hash = null!;
        app.Use(async (ctx, next) =>
        {
            _ = ctx.Response.Cookies;
            await next(ctx);
        });
        if (policy)
            app.UseCookiePolicy(new CookiePolicyOptions { MinimumSameSitePolicy = SameSiteMode.Strict });
        app.Use(async (ctx, next) =>
        {
            var cookies = ctx.Response.Cookies;
            var firstAttempt = executions == 0;
            ctx.Response.OnStarting(() =>
            {
                if (firstAttempt)
                    cookies.Append("callback-cookie", "first");
                return Task.CompletedTask;
            });
            ctx.SetEndpoint(new Microsoft.AspNetCore.Http.Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(definition), "financial"));
            identity = FinancialIdentity.Build(ctx.Request, opts, "account");
            hash = await FinancialPayloadHash.ComputeAsync(ctx.Request, CancellationToken.None);
            await next(ctx);
        });
        app.UseFinancialIdempotency();
        app.Run(async ctx =>
        {
            executions++;
            ctx.Response.StatusCode = 201;
            ctx.Response.Cookies.Append("business-cookie", "abc");
            if (delete)
                ctx.Response.Cookies.Delete("business-cookie");
            await ctx.Response.WriteAsync("result");
        });
        await app.StartAsync();
        using var client = new HttpClient(new HttpClientHandler { UseCookies = false });
        client.DefaultRequestHeaders.Add("Idempotency-Key", "same");
        using var first = await client.PostAsync(app.Urls.Single(), new StringContent("same"));
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await first.Content.ReadAsStringAsync()).ShouldBe("result");
        var cookies = first.Headers.GetValues("Set-Cookie").ToArray();
        cookies.Length.ShouldBe(2);
        var business = cookies.Single(c => c.StartsWith("business-cookie=", StringComparison.Ordinal));
        business.ShouldStartWith(delete ? "business-cookie=;" : "business-cookie=abc;");
        if (delete)
            business.ShouldContain("expires=Thu, 01 Jan 1970 00:00:00 GMT");
        cookies.ShouldContain(c => c.StartsWith("callback-cookie=first;", StringComparison.Ordinal));
        if (policy)
            cookies.ShouldAllBe(c => c.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
        var stored = await store.TryBeginAsync(identity, hash, opts.Duration, CancellationToken.None);
        stored.Kind.ShouldBe(FinancialBeginKind.Replay);
        stored.Response!.Headers.ShouldNotContain(h => h.Key == "Set-Cookie");
        using var replay = await client.PostAsync(app.Urls.Single(), new StringContent("same"));
        replay.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await replay.Content.ReadAsStringAsync()).ShouldBe("result");
        replay.Headers.Contains("Set-Cookie").ShouldBeFalse();
        executions.ShouldBe(1);
    }

    [Fact]
    public async Task Kestrel_Nonfinancial_Callbacks_Keep_Their_Order_And_Completion()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddFinancialIdempotency();
        await using var app = builder.Build();
        var callbacks = new List<int>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Use(async (ctx, next) =>
        {
            ctx.Response.OnStarting(() =>
            {
                callbacks.Add(1);
                ctx.Response.StatusCode = 202;
                return Task.CompletedTask;
            });
            ctx.Response.OnCompleted(() => { completed.SetResult(); return Task.CompletedTask; });
            await next(ctx);
        });
        app.UseFinancialIdempotency();
        app.Run(async ctx =>
        {
            ctx.Response.OnStarting(() => { callbacks.Add(2); return Task.CompletedTask; });
            await ctx.Response.WriteAsync("normal");
        });
        await app.StartAsync();
        using var client = new HttpClient();
        using var response = await client.GetAsync(app.Urls.Single());
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await response.Content.ReadAsStringAsync()).ShouldBe("normal");
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        callbacks.ShouldBe([2, 1]);
    }

    [Fact]
    public async Task Capture_Preserves_Upstream_Callbacks_Registered_During_Starting()
    {
        var response = new FinancialResponseFeature(new HttpResponseFeature());
        await using var capture = new FinancialResponseCapture(response, 1024);
        var callbacks = new List<int>();
        response.OnStarting(_ => { callbacks.Add(1); return Task.CompletedTask; }, response);
        response.OnStarting(_ =>
        {
            callbacks.Add(2);
            capture.OnStarting(_ => { callbacks.Add(3); return Task.CompletedTask; }, capture);
            return Task.CompletedTask;
        }, response);
        response.TransferStartingTo(capture);
        await capture.SnapshotAsync();
        await response.RunStartingAsync();
        callbacks.ShouldBe([2, 3, 1]);
    }

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
        var store = new MemoryFinancialIdempotencyStore();
        builder.Services.AddFinancialIdempotency(store);
        await using var app = builder.Build();
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
    public async Task Capture_Seals_Shared_Headers_After_Starting_Callbacks()
    {
        var original = new HttpResponseFeature();
        original.Headers["X-Final"] = "initial";
        await using var capture = new FinancialResponseCapture(original, 1024);
        capture.OnStarting(_ =>
        {
            original.Headers["X-Final"] = "callback";
            return Task.CompletedTask;
        }, original);

        await capture.StartAsync();

        capture.Headers["X-Final"].ToString().ShouldBe("callback");
        Should.Throw<InvalidOperationException>(() => capture.Headers["X-Final"] = "late");
        original.Headers["X-Final"] = "transport";
        capture.Headers["X-Final"].ToString().ShouldBe("callback");
    }

    [Fact]
    public async Task Capture_Rejects_Overflow_While_Writing()
    {
        await using var capture = new FinancialResponseCapture(new HttpResponseFeature(), 4);
        await Should.ThrowAsync<IOException>(async () => await capture.Stream.WriteAsync(new byte[5]));
    }

    [Fact]
    public async Task Successful_Handler_Completes_Even_If_Client_Aborted()
    {
        var store = new MemoryFinancialIdempotencyStore();
        var definition = new EndpointDefinition(typeof(FinancialResponseCaptureTests), typeof(object), typeof(object));
        definition.FinancialIdempotency(o => o.CallerScope = _ => "account");
        var body = "charged"u8.ToArray();
        var executions = 0;
        using var abort = new CancellationTokenSource();
        var middleware = new FinancialIdempotencyMiddleware(
            async ctx =>
            {
                executions++;
                ctx.Response.StatusCode = 201;
                await ctx.Response.Body.WriteAsync(body);
                abort.Cancel();
            },
            store,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FinancialIdempotencyMiddleware>.Instance);

        var first = CreateContext();
        first.RequestAborted = abort.Token;

        try
        {
            await middleware.Invoke(first);
        }
        catch (OperationCanceledException) { }

        var replay = CreateContext();
        await middleware.Invoke(replay);
        replay.Response.StatusCode.ShouldBe(201);
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
    public async Task Abort_During_Handler_Remains_Unreplayable()
    {
        var store = new MemoryFinancialIdempotencyStore();
        var definition = new EndpointDefinition(typeof(FinancialResponseCaptureTests), typeof(object), typeof(object));
        definition.FinancialIdempotency(o => o.CallerScope = _ => "account");
        var executions = 0;
        using var abort = new CancellationTokenSource();
        var middleware = new FinancialIdempotencyMiddleware(
            _ =>
            {
                executions++;
                abort.Cancel();

                return Task.FromException(new OperationCanceledException(abort.Token));
            },
            store,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FinancialIdempotencyMiddleware>.Instance);

        var first = CreateContext();
        first.RequestAborted = abort.Token;
        await Should.ThrowAsync<OperationCanceledException>(() => middleware.Invoke(first));

        var retry = CreateContext();
        await middleware.Invoke(retry);
        retry.Response.StatusCode.ShouldBe(500);
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
    public async Task Store_Begin_Without_Token_Or_Replay_Body_Does_Not_Run_The_Handler()
    {
        var definition = new EndpointDefinition(typeof(FinancialResponseCaptureTests), typeof(object), typeof(object));
        definition.FinancialIdempotency(o => o.CallerScope = _ => "account");
        var executions = 0;
        var store = new BrokenBeginStore();
        var middleware = new FinancialIdempotencyMiddleware(
            _ =>
            {
                executions++;

                return Task.CompletedTask;
            },
            store,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FinancialIdempotencyMiddleware>.Instance);

        var missingToken = Context();
        await middleware.Invoke(missingToken);
        missingToken.Response.StatusCode.ShouldBe(500);

        store.WhitespaceToken = true;
        var whitespaceToken = Context();
        await middleware.Invoke(whitespaceToken);
        whitespaceToken.Response.StatusCode.ShouldBe(500);

        store.ReplayWithoutBody = true;
        var missingBody = Context();
        await middleware.Invoke(missingBody);
        missingBody.Response.StatusCode.ShouldBe(500);
        executions.ShouldBe(0);

        DefaultHttpContext Context()
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = "POST";
            ctx.Request.Headers["Idempotency-Key"] = "same";
            ctx.Response.Body = new MemoryStream();
            ctx.SetEndpoint(new Microsoft.AspNetCore.Http.Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(definition), "financial"));

            return ctx;
        }
    }

    sealed class BrokenBeginStore : IFinancialIdempotencyStore
    {
        public bool ReplayWithoutBody { get; set; }
        public bool WhitespaceToken { get; set; }

        public ValueTask<FinancialBeginResult> TryBeginAsync(string identityKey, ReadOnlyMemory<byte> payloadHash, TimeSpan ttl, CancellationToken ct)
            => new(ReplayWithoutBody
                       ? FinancialBeginResult.Replay(null!)
                       : FinancialBeginResult.Started(WhitespaceToken ? "  " : ""));

        public Task<FinancialSettlementResult> CompleteAsync(string identityKey, string reservationToken, FinancialIdempotencyResponse response, TimeSpan ttl, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<FinancialSettlementResult> MarkUnreplayableAsync(string identityKey, string reservationToken, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<FinancialSettlementResult> AbandonAsync(string identityKey, string reservationToken, CancellationToken ct)
            => throw new NotSupportedException();
    }
}
