using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Xunit;

namespace Warmup;

[CollectionDefinition(Name, DisableParallelization = true)]
public class WarmupTestCollection
{
    public const string Name = nameof(WarmupTestCollection);
}

[Collection(WarmupTestCollection.Name)]
public class WarmupTests : IDisposable
{
    static readonly SemaphoreSlim _testLock = new(1, 1);

    readonly Func<EndpointDefinition, bool>? _previousEndpointFilter;
    readonly Func<EndpointDefinition, bool>? _previousWarmupFilter;
    readonly bool _previousWarmupRequested;
    readonly JsonSerializerOptions _previousSerializerOptions;
    readonly bool _previousSerializerConfigured;

    public WarmupTests()
    {
        _testLock.Wait();

        _previousEndpointFilter = Config.EpOpts.Filter;
        _previousWarmupFilter = Config.EpOpts.WarmupFilter;
        _previousWarmupRequested = Config.EpOpts.WarmupRequested;
        _previousSerializerOptions = Config.SerOpts.Options;
        _previousSerializerConfigured = MainExtensions.SerializerConfigured;

        ResetState();
    }

    public void Dispose()
    {
        Config.EpOpts.Filter = _previousEndpointFilter;
        Config.EpOpts.WarmupFilter = _previousWarmupFilter;
        Config.EpOpts.WarmupRequested = _previousWarmupRequested;
        Config.SerOpts.Options = _previousSerializerOptions;
        MainExtensions.SerializerConfigured = _previousSerializerConfigured;
        CountingBinder.Reset();
        ResetServiceResolver();
        _testLock.Release();
    }

    static void ResetState()
    {
        Config.EpOpts.Filter = null;
        Config.EpOpts.WarmupFilter = null;
        Config.EpOpts.WarmupRequested = false;
        Config.SerOpts.Options = new();
        MainExtensions.SerializerConfigured = false;
        CountingBinder.Reset();
        ResetServiceResolver();
    }

    [Fact]
    public void Warmup_SetsWarmupRequestedAndFilter()
    {
        var opts = new EndpointOptions();

        static bool Filter(EndpointDefinition _)
            => true;

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
            app.UseFastEndpoints(
                c =>
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

    [Fact]
    public async Task Warmup_PrecompilesNestedComplexAndCollectionElementTypeAccessors()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(WarmupNestedEp)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.Warmup());

            Config.BndOpts.ReflectionCache.TryGetValue(typeof(NestedDto), out var nestedDef).ShouldBeTrue();
            nestedDef!.ObjectFactory.ShouldNotBeNull();
            nestedDef.Properties!.TryGetValue(typeof(NestedDto).GetProperty(nameof(NestedDto.City))!, out var cityDef).ShouldBeTrue();
            cityDef!.Getter.ShouldNotBeNull();
            cityDef.Setter.ShouldNotBeNull();

            Config.BndOpts.ReflectionCache.TryGetValue(typeof(NestedItemDto), out var itemDef).ShouldBeTrue();
            itemDef!.Properties!.TryGetValue(typeof(NestedItemDto).GetProperty(nameof(NestedItemDto.Qty))!, out var qtyDef).ShouldBeTrue();
            qtyDef!.Getter.ShouldNotBeNull();
            qtyDef.Setter.ShouldNotBeNull();
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
    {
        CountingBinder.InstanceCount++;
    }

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
    public override void Configure()
        => Get("warmup-ep-a");

    public override Task HandleAsync(CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class WarmupEpB : EndpointWithoutRequest
{
    public override void Configure()
        => Get("warmup-ep-b");

    public override Task HandleAsync(CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class WarmupNestedRequest
{
    [Required]
    public string? Name { get; set; }

    public NestedDto? Nested { get; set; }

    public List<NestedItemDto>? Items { get; set; }
}

file sealed class NestedDto
{
    [Required]
    public string? City { get; set; }
}

file sealed class NestedItemDto
{
    [Range(1, 10)]
    public int Qty { get; set; }
}

file sealed class WarmupNestedEp : Endpoint<WarmupNestedRequest>
{
    public override void Configure()
        => Post("warmup-nested-ep");

    public override Task HandleAsync(WarmupNestedRequest req, CancellationToken ct)
        => Task.CompletedTask;
}