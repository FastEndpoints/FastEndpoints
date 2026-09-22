using System.Net;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RouteMapper;
using Xunit;

namespace Unit.FastEndpoints;

public class FinancialIdempotencyEndpointDefinitionTests
{
    [Fact]
    public void Financial_After_Fingerprint_Throws()
    {
        var ex = Should.Throw<InvalidOperationException>(() => Factory.Create<FingerprintThenFinancialEndpoint>());
        ex.Message.ShouldContain("FinancialIdempotency()");
        ex.Message.ShouldContain("Idempotency()");
    }

    [Fact]
    public void Fingerprint_After_Financial_Throws()
    {
        var ex = Should.Throw<InvalidOperationException>(() => Factory.Create<FinancialThenFingerprintEndpoint>());
        ex.Message.ShouldContain("Idempotency()");
        ex.Message.ShouldContain("FinancialIdempotency()");
    }
}

[Collection(RouteMapperCollection.Name)]
public class FinancialIdempotencyMapperTests : IDisposable
{
    readonly Func<EndpointDefinition, bool>? _previousFilter;
    readonly string? _previousRoutePrefix;
    readonly Action<EndpointDefinition>? _previousConfigurator;
    readonly bool _previousSerializerConfigured;

    public FinancialIdempotencyMapperTests()
    {
        _previousFilter = Config.EpOpts.Filter;
        _previousRoutePrefix = Config.EpOpts.RoutePrefix;
        _previousConfigurator = Config.EpOpts.Configurator;
        _previousSerializerConfigured = MainExtensions.SerializerConfigured;
        Config.EpOpts.Filter = null;
        Config.EpOpts.RoutePrefix = null;
        Config.EpOpts.Configurator = null;
        MainExtensions.SerializerConfigured = false;
    }

    [Fact]
    public async Task Middleware_Before_Routing_Does_Not_Run_The_Handler()
    {
        GuardedFinancialEndpoint.Hits = 0;
        await using var app = GuardHost(financialBeforeRouting: true);
        await app.StartAsync();
        using var client = new HttpClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, app.Urls.Single().TrimEnd('/') + "/fin/guard");
        req.Headers.TryAddWithoutValidation("Idempotency-Key", "before-routing");

        var res = await client.SendAsync(req);

        ((int)res.StatusCode).ShouldBe(500);
        GuardedFinancialEndpoint.Hits.ShouldBe(0);
    }

    [Fact]
    public async Task Middleware_After_Routing_Runs_Once_And_Replays()
    {
        GuardedFinancialEndpoint.Hits = 0;
        await using var app = GuardHost(financialBeforeRouting: false);
        await app.StartAsync();
        using var client = new HttpClient();
        var url = app.Urls.Single().TrimEnd('/') + "/fin/guard";

        (await Post()).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Post()).StatusCode.ShouldBe(HttpStatusCode.OK);
        GuardedFinancialEndpoint.Hits.ShouldBe(1);

        Task<HttpResponseMessage> Post()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.TryAddWithoutValidation("Idempotency-Key", "after-routing");

            return client.SendAsync(req);
        }
    }

    static WebApplication GuardHost(bool financialBeforeRouting)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddFastEndpoints([typeof(GuardedFinancialEndpoint)]);
        builder.Services.AddFinancialIdempotency(c => c.CallerScope = _ => "account");
        var app = builder.Build();

        if (financialBeforeRouting)
        {
            app.UseFinancialIdempotency();
            app.UseRouting();
        }
        else
        {
            app.UseRouting();
            app.UseFinancialIdempotency();
        }

        app.UseFastEndpoints();

        return app;
    }

    public void Dispose()
    {
        Config.EpOpts.Filter = _previousFilter;
        Config.EpOpts.RoutePrefix = _previousRoutePrefix;
        Config.EpOpts.Configurator = _previousConfigurator;
        MainExtensions.SerializerConfigured = _previousSerializerConfigured;

        var testingProvider = new ServiceCollection().AddHttpContextAccessor().BuildServiceProvider();
        ServiceResolver.Instance = new ServiceResolver(
            provider: testingProvider,
            ctxAccessor: testingProvider.GetRequiredService<IHttpContextAccessor>(),
            isUnitTestMode: true);
    }

    [Fact]
    public async Task Registration_Is_Local_And_Duplicate_Insertion_Throws()
    {
        await using var first = WebApplication.CreateBuilder().Build();
        await using var second = WebApplication.CreateBuilder().Build();
        first.UseFinancialIdempotency();
        Should.Throw<InvalidOperationException>(() => first.UseFinancialIdempotency());
        Should.NotThrow(() => second.UseFinancialIdempotency());
        var branch = ((IApplicationBuilder)first).New();
        Should.Throw<InvalidOperationException>(() => branch.UseFinancialIdempotency());
    }

    [Fact]
    public async Task Mapping_Throws_When_Middleware_Is_Missing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(FinancialOnlyEndpoint)]);
        builder.Services.AddFinancialIdempotency(c => c.CallerScope = _ => "test");
        var app = builder.Build();

        try
        {
            var ex = Should.Throw<InvalidOperationException>(() => app.UseFastEndpoints());
            ex.Message.ShouldBe("Financial idempotency middleware setup is incorrect!");
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Mapping_Throws_When_Store_Is_Missing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(FinancialOnlyEndpoint)]);
        var app = builder.Build();
        app.UseFinancialIdempotency();

        try
        {
            var ex = Should.Throw<InvalidOperationException>(() => app.UseFastEndpoints());
            ex.Message.ShouldBe("Financial idempotency middleware setup is incorrect!");
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Mapping_Succeeds_When_Store_And_Middleware_Are_Registered()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(FinancialOnlyEndpoint)]);
        builder.Services.AddFinancialIdempotency(c => c.CallerScope = _ => "test");
        var app = builder.Build();
        app.UseFinancialIdempotency();

        try
        {
            Should.NotThrow(() => app.UseFastEndpoints());
        }
        finally
        {
            await app.DisposeAsync();
        }
    }
}

file sealed class FingerprintThenFinancialEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("fin/fingerprint-then-financial");
        Idempotency();
        FinancialIdempotency();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class FinancialThenFingerprintEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("fin/financial-then-fingerprint");
        FinancialIdempotency();
        Idempotency();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class FinancialOnlyEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("fin/only");
        AllowAnonymous();
        FinancialIdempotency();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class GuardedFinancialEndpoint : EndpointWithoutRequest
{
    internal static int Hits;

    public override void Configure()
    {
        Post("fin/guard");
        AllowAnonymous();
        FinancialIdempotency();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref Hits);

        return HttpContext.Response.WriteAsync("ok", ct);
    }
}
