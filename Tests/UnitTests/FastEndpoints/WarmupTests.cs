using FakeItEasy;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Warmup;

public class WarmupTests : IDisposable
{
    public void Dispose()
    {
        Config.EpOpts.Filter = null;
        Config.EpOpts.WarmupFilter = null;
        Config.EpOpts.WarmupRequested = false;
        CountingBinder.Reset();
    }

    [Fact]
    public void WarmUp_SetsWarmupRequested()
    {
        var opts = new EndpointOptions();

        opts.WarmupRequested.ShouldBeFalse();

        opts.WarmUp();

        opts.WarmupRequested.ShouldBeTrue();
    }

    [Fact]
    public void WarmupFilter_Null_WarmsAllEndpointsWithoutInstantiatingEndpoints()
    {
        var factory = A.Fake<IEndpointFactory>();
        var sp = BuildServiceProvider(factory, [typeof(WarmupEpA), typeof(WarmupEpB)]);

        MainExtensions.Warmup(sp);

        CountingBinder.InstanceCount.ShouldBe(2);
        A.CallTo(() => factory.Create(A<EndpointDefinition>._, A<HttpContext>._)).MustNotHaveHappened();
    }

    [Fact]
    public void WarmupFilter_ReturnsFalseForEndpoint_SkipsItsWarmup()
    {
        Config.EpOpts.WarmupFilter = def => def.EndpointType == typeof(WarmupEpA);
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
    public void WarmupFilter_AlwaysFalse_SkipsAllEndpoints()
    {
        Config.EpOpts.WarmupFilter = _ => false;
        var factory = A.Fake<IEndpointFactory>();
        var sp = BuildServiceProvider(factory, [typeof(WarmupEpA), typeof(WarmupEpB)]);

        MainExtensions.Warmup(sp);

        CountingBinder.InstanceCount.ShouldBe(0);
        A.CallTo(() => factory.Create(A<EndpointDefinition>._, A<HttpContext>._)).MustNotHaveHappened();
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
