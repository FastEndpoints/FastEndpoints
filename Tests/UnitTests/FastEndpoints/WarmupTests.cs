using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace Warmup;

public class WarmupTests : IDisposable
{
    public void Dispose()
    {
        Config.EpOpts.Filter = null;
        Config.EpOpts.WarmupFilter = null;
        Config.EpOpts.WarmupRequested = false;
        Config.SerOpts.Options = new JsonSerializerOptions();
        CountingBinder.Reset();
        ResetServiceResolver();
    }

    [Fact]
    public void Warmup_SetsWarmupRequestedAndFilter()
    {
        var opts = new EndpointOptions();
        static bool Filter(EndpointDefinition _) => true;

        opts.WarmupRequested.ShouldBeFalse();
        opts.WarmupFilter.ShouldBeNull();

        opts.Warmup(Filter);

        opts.WarmupRequested.ShouldBeTrue();
        opts.WarmupFilter.ShouldBe(Filter);
    }

    [Fact]
    public async Task Warmup_FilterNull_WarmsAllEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddTransient(typeof(IRequestBinder<>), typeof(CountingBinder<>));
        builder.Services.AddFastEndpoints([typeof(WarmupEpA), typeof(WarmupEpB)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.Warmup());

            CountingBinder.InstanceCount.ShouldBe(2);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Warmup_FilterReturnsTrueForEndpoint_WarmsOnlyThatEndpoint()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddTransient(typeof(IRequestBinder<>), typeof(CountingBinder<>));
        builder.Services.AddFastEndpoints([typeof(WarmupEpA), typeof(WarmupEpB)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.Warmup(def => def.EndpointType == typeof(WarmupEpA)));

            CountingBinder.InstanceCount.ShouldBe(1);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task EndpointFilter_ExcludesEndpoint_SkipsItsWarmup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddTransient(typeof(IRequestBinder<>), typeof(CountingBinder<>));
        builder.Services.AddFastEndpoints([typeof(WarmupEpA), typeof(WarmupEpB)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c =>
            {
                c.Endpoints.Filter = def => def.EndpointType == typeof(WarmupEpA);
                c.Endpoints.Warmup();
            });

            CountingBinder.InstanceCount.ShouldBe(1);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Warmup_FilterAlwaysFalse_SkipsAllEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddTransient(typeof(IRequestBinder<>), typeof(CountingBinder<>));
        builder.Services.AddFastEndpoints([typeof(WarmupEpA), typeof(WarmupEpB)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.Warmup(_ => false));

            CountingBinder.InstanceCount.ShouldBe(0);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task UseFastEndpoints_WarmupFilter_WarmsOnlyMatchingEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddTransient(typeof(IRequestBinder<>), typeof(CountingBinder<>));
        builder.Services.AddFastEndpoints([typeof(WarmupEpA), typeof(WarmupEpB)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.Warmup(def => def.EndpointType == typeof(WarmupEpB)));

            CountingBinder.InstanceCount.ShouldBe(1);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    static void ResetServiceResolver()
    {
        var testingProvider = new ServiceCollection().AddHttpContextAccessor().BuildServiceProvider();
        ServiceResolver.Instance = new ServiceResolver(
            provider: testingProvider,
            ctxAccessor: testingProvider.GetRequiredService<IHttpContextAccessor>(),
            isUnitTestMode: true);
    }
}

file sealed class CountingBinder<TRequest> : IRequestBinder<TRequest>
    where TRequest : notnull
{
    public CountingBinder()
        => CountingBinder.InstanceCount++;

    public ValueTask<TRequest> BindAsync(BinderContext ctx, CancellationToken ct)
        => ValueTask.FromResult(default(TRequest)!);
}

file static class CountingBinder
{
    public static int InstanceCount { get; set; }

    public static void Reset()
        => InstanceCount = 0;
}

file sealed class WarmupEpA : EndpointWithoutRequest
{
    public override void Configure() => Get("warmup-ep-a");
    public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
}

file sealed class WarmupEpB : EndpointWithoutRequest
{
    public override void Configure() => Get("warmup-ep-b");
    public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
}
