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
        // Reset global warmup filter so other tests are not affected.
        Config.EpOpts.WarmupFilter = null;
    }

    [Fact]
    public void WarmupFilter_Null_WarmsAllEndpoints()
    {
        Config.EpOpts.WarmupFilter = null;
        var factory = A.Fake<IEndpointFactory>();
        var sp = BuildServiceProvider(factory, [typeof(WarmupEpA), typeof(WarmupEpB)]);

        MainExtensions.Warmup(sp);

        A.CallTo(() => factory.Create(A<EndpointDefinition>.That.Matches(d => d.EndpointType == typeof(WarmupEpA)), A<HttpContext>.That.Matches(c => c.RequestServices == sp)))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => factory.Create(A<EndpointDefinition>.That.Matches(d => d.EndpointType == typeof(WarmupEpB)), A<HttpContext>.That.Matches(c => c.RequestServices == sp)))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void WarmupFilter_ReturnsFalseForEndpoint_SkipsItsWarmup()
    {
        Config.EpOpts.WarmupFilter = def => def.EndpointType == typeof(WarmupEpA);
        var factory = A.Fake<IEndpointFactory>();
        var sp = BuildServiceProvider(factory, [typeof(WarmupEpA), typeof(WarmupEpB)]);

        MainExtensions.Warmup(sp);

        A.CallTo(() => factory.Create(A<EndpointDefinition>.That.Matches(d => d.EndpointType == typeof(WarmupEpA)), A<HttpContext>.That.Matches(c => c.RequestServices == sp)))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => factory.Create(A<EndpointDefinition>.That.Matches(d => d.EndpointType == typeof(WarmupEpB)), A<HttpContext>.That.Matches(c => c.RequestServices == sp)))
            .MustNotHaveHappened();
    }

    [Fact]
    public void WarmupFilter_AlwaysFalse_SkipsAllEndpoints()
    {
        Config.EpOpts.WarmupFilter = _ => false;
        var factory = A.Fake<IEndpointFactory>();
        var sp = BuildServiceProvider(factory, [typeof(WarmupEpA), typeof(WarmupEpB)]);

        MainExtensions.Warmup(sp);

        A.CallTo(() => factory.Create(A<EndpointDefinition>._, A<HttpContext>._)).MustNotHaveHappened();
    }

    static IServiceProvider BuildServiceProvider(IEndpointFactory factory, IEnumerable<Type> endpointTypes)
    {
        var endpointData = new EndpointData(endpointTypes, new());
        var services = new ServiceCollection();
        services.AddSingleton(endpointData);
        services.AddSingleton(factory);
        return services.BuildServiceProvider();
    }
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
