using FakeItEasy;
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
    public void WarmUp_SetsWarmupRequestedAndFilter()
    {
        var opts = new EndpointOptions();
        static bool Filter(EndpointDefinition _) => true;

        opts.WarmupRequested.ShouldBeFalse();
        opts.WarmupFilter.ShouldBeNull();

        opts.WarmUp(Filter);

        opts.WarmupRequested.ShouldBeTrue();
        opts.WarmupFilter.ShouldBe(Filter);
    }

    [Fact]
    public void WarmUp_FilterNull_WarmsAllEndpointsWithoutInstantiatingEndpoints()
    {
        var factory = A.Fake<IEndpointFactory>();
        var sp = BuildServiceProvider(factory, [typeof(WarmupEpA), typeof(WarmupEpB)]);

        MainExtensions.Warmup(sp);

        CountingBinder.InstanceCount.ShouldBe(2);
        A.CallTo(() => factory.Create(A<EndpointDefinition>._, A<HttpContext>._)).MustNotHaveHappened();
    }

    [Fact]
    public void WarmUp_FilterReturnsFalseForEndpoint_SkipsItsWarmup()
    {
        Config.EpOpts.WarmUp(def => def.EndpointType == typeof(WarmupEpA));
        var factory = A.Fake<IEndpointFactory>();
        var sp = BuildServiceProvider(factory, [typeof(WarmupEpA), typeof(WarmupEpB)]);

        MainExtensions.Warmup(sp);

        CountingBinder.InstanceCount.ShouldBe(1);
        A.CallTo(() => factory.Create(A<EndpointDefinition>._, A<HttpContext>._)).MustNotHaveHappened();
    }

    [Fact]
    public void EndpointFilter_ReturnsFalseForEndpoint_SkipsItsWarmup()
    {
        Config.EpOpts.Filter = def => def.EndpointType == typeof(WarmupEpA);
        var factory = A.Fake<IEndpointFactory>();
        var sp = BuildServiceProvider(factory, [typeof(WarmupEpA), typeof(WarmupEpB)]);

        MainExtensions.Warmup(sp);

        CountingBinder.InstanceCount.ShouldBe(1);
        A.CallTo(() => factory.Create(A<EndpointDefinition>._, A<HttpContext>._)).MustNotHaveHappened();
    }

    [Fact]
    public void WarmUp_FilterAlwaysFalse_SkipsAllEndpoints()
    {
        Config.EpOpts.WarmUp(_ => false);
        var factory = A.Fake<IEndpointFactory>();
        var sp = BuildServiceProvider(factory, [typeof(WarmupEpA), typeof(WarmupEpB)]);

        MainExtensions.Warmup(sp);

        CountingBinder.InstanceCount.ShouldBe(0);
        A.CallTo(() => factory.Create(A<EndpointDefinition>._, A<HttpContext>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task UseFastEndpoints_WarmUpFilter_WarmsOnlyMatchingEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddTransient(typeof(IRequestBinder<>), typeof(CountingBinder<>));
        builder.Services.AddFastEndpoints([typeof(WarmupEpA), typeof(WarmupEpB)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(c => c.Endpoints.WarmUp(def => def.EndpointType == typeof(WarmupEpB)));

            CountingBinder.InstanceCount.ShouldBe(1);
        }
        finally
        {
            ResetServiceResolver();
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

    static IServiceProvider BuildServiceProvider(IEndpointFactory factory, IEnumerable<Type> endpointTypes)
    {
        var endpointData = new EndpointData(endpointTypes, new());
        var services = new ServiceCollection();
        services.AddSingleton(endpointData);
        services.AddSingleton(factory);
        services.AddTransient(typeof(IRequestBinder<>), typeof(CountingBinder<>));
        return services.BuildServiceProvider();
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
