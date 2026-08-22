using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace RouteMapper;

[CollectionDefinition(RouteMapperCollection.Name, DisableParallelization = true)]
public class RouteMapperCollection
{
    public const string Name = nameof(RouteMapperCollection);
}

[Collection(RouteMapperCollection.Name)]
public class EndpointRouteMapperTests : IDisposable
{
    readonly Func<EndpointDefinition, bool>? _previousEndpointFilter;
    readonly string? _previousRoutePrefix;
    readonly Action<EndpointDefinition>? _previousConfigurator;
    readonly int _previousDefaultVersion;
    readonly bool? _previousPrependToRoute;
    readonly string? _previousVersionPrefix;
    readonly string? _previousRouteTemplate;
    readonly bool _previousSerializerConfigured;

    public EndpointRouteMapperTests()
    {
        _previousEndpointFilter = Config.EpOpts.Filter;
        _previousRoutePrefix = Config.EpOpts.RoutePrefix;
        _previousConfigurator = Config.EpOpts.Configurator;
        _previousDefaultVersion = Config.VerOpts.DefaultVersion;
        _previousPrependToRoute = Config.VerOpts.PrependToRoute;
        _previousVersionPrefix = Config.VerOpts.Prefix;
        _previousRouteTemplate = Config.VerOpts.RouteTemplate;
        _previousSerializerConfigured = MainExtensions.SerializerConfigured;
        Config.EpOpts.Filter = null;
        Config.EpOpts.RoutePrefix = null;
        Config.EpOpts.Configurator = null;
        Config.VerOpts.DefaultVersion = 0;
        Config.VerOpts.PrependToRoute = null;
        Config.VerOpts.Prefix = null;
        Config.VerOpts.RouteTemplate = null;
        MainExtensions.SerializerConfigured = false;
        LockStateRecordingEp.ObservedLockStates.Clear();
    }

    public void Dispose()
    {
        Config.EpOpts.Filter = _previousEndpointFilter;
        Config.EpOpts.RoutePrefix = _previousRoutePrefix;
        Config.EpOpts.Configurator = _previousConfigurator;
        Config.VerOpts.DefaultVersion = _previousDefaultVersion;
        Config.VerOpts.PrependToRoute = _previousPrependToRoute;
        Config.VerOpts.Prefix = _previousVersionPrefix;
        Config.VerOpts.RouteTemplate = _previousRouteTemplate;
        MainExtensions.SerializerConfigured = _previousSerializerConfigured;
        LockStateRecordingEp.ObservedLockStates.Clear();

        var testingProvider = new ServiceCollection().AddHttpContextAccessor().BuildServiceProvider();
        ServiceResolver.Instance = new ServiceResolver(
            provider: testingProvider,
            ctxAccessor: testingProvider.GetRequiredService<IHttpContextAccessor>(),
            isUnitTestMode: true);
    }

    [Fact]
    public async Task ForwardedAttributes_AreAddedToEveryRouteOfAMultiRouteEndpoint()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(MultiRouteAttribEp)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints();

            var routes = EndpointsOf<MultiRouteAttribEp>(app);
            routes.Length.ShouldBe(2);

            foreach (var route in routes)
            {
                //the attribute is not recognized by the endpoint configurator, so it goes into AttribsToForward and must be
                //forwarded to the metadata of every route of the endpoint - not just the first one.
                route.Metadata
                     .OfType<ForwardedMarkerAttribute>()
                     .Count()
                     .ShouldBe(1, $"forwarded attribute missing from route [{route.RoutePattern.RawText}]");
            }

            routes.Select(r => r.RoutePattern.RawText).ShouldBe(["multi-route-forwarding/one", "multi-route-forwarding/two"], ignoreOrder: true);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Definition_IsNotLockedUntilAllRoutesAreMapped()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(LockStateRecordingEp)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints();

            //the user config action runs once per mapped route, while mapping is still in progress
            LockStateRecordingEp.ObservedLockStates.Count.ShouldBe(2);
            LockStateRecordingEp.ObservedLockStates.ShouldAllBe(locked => locked == false);

            //and the definition is locked once mapping of the endpoint is fully done. every route of an endpoint shares
            //the very same definition instance, so the lock state is asserted once - not per route.
            var routes = EndpointsOf<LockStateRecordingEp>(app);
            routes.Length.ShouldBe(2);

            var definitions = routes.Select(r => r.Metadata.GetMetadata<EndpointDefinition>()).Distinct().ToArray();
            definitions.Length.ShouldBe(1);
            definitions[0]!.IsLocked.ShouldBeTrue();
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task DontVersion_OmitsVersionSegmentWhenDefaultVersionIsSet()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(DontVersionEp), typeof(DefaultVersionedEp)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(
                c =>
                {
                    c.Versioning.Prefix = "v";
                    c.Versioning.DefaultVersion = 1;
                    c.Versioning.PrependToRoute = true;
                });

            EndpointsOf<DontVersionEp>(app)
               .Select(r => r.RoutePattern.RawText)
               .ShouldBe(["dont-version-me"]);

            EndpointsOf<DefaultVersionedEp>(app)
               .Select(r => r.RoutePattern.RawText)
               .ShouldBe(["v1/do-version-me"]);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task DontVersion_FromConfigurator_OmitsVersionSegment()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(ConfiguratorDontVersionEp)]);
        var app = builder.Build();

        try
        {
            app.UseFastEndpoints(
                c =>
                {
                    c.Versioning.Prefix = "v";
                    c.Versioning.DefaultVersion = 1;
                    c.Versioning.PrependToRoute = true;
                    c.Endpoints.Configurator = ep =>
                    {
                        if (ep.EndpointType == typeof(ConfiguratorDontVersionEp))
                            ep.DontVersion();
                    };
                });

            EndpointsOf<ConfiguratorDontVersionEp>(app)
               .Select(r => r.RoutePattern.RawText)
               .ShouldBe(["configurator-dont-version-me"]);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    static RouteEndpoint[] EndpointsOf<TEndpoint>(WebApplication app)
        => ((IEndpointRouteBuilder)app).DataSources
                                       .SelectMany(ds => ds.Endpoints)
                                       .OfType<RouteEndpoint>()
                                       .Where(e => e.Metadata.GetMetadata<EndpointDefinition>()?.EndpointType == typeof(TEndpoint))
                                       .ToArray();
}

[AttributeUsage(AttributeTargets.Class)]
file sealed class ForwardedMarkerAttribute : Attribute;

[HttpGet("multi-route-forwarding/one", "multi-route-forwarding/two"), ForwardedMarker, AllowAnonymous]
file sealed class MultiRouteAttribEp : EndpointWithoutRequest
{
    public override Task HandleAsync(CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class LockStateRecordingEp : EndpointWithoutRequest
{
    public static readonly List<bool> ObservedLockStates = [];

    public override void Configure()
    {
        Get("lock-state-recording/one", "lock-state-recording/two");
        AllowAnonymous();
        Options(_ => ObservedLockStates.Add(Definition.IsLocked));
    }

    public override Task HandleAsync(CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class DontVersionEp : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("dont-version-me");
        AllowAnonymous();
        DontVersion();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class DefaultVersionedEp : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("do-version-me");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class ConfiguratorDontVersionEp : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("configurator-dont-version-me");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Task.CompletedTask;
}
