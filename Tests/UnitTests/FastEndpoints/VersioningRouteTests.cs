using System.Text;
using Xunit;

namespace FastEndpoints;

public class VersioningRouteTests : IDisposable
{
    readonly int _previousDefaultVersion;
    readonly bool? _previousPrependToRoute;
    readonly string? _previousPrefix;
    readonly string? _previousRouteTemplate;
    readonly string? _previousRoutePrefix;

    public VersioningRouteTests()
    {
        _previousDefaultVersion = Config.VerOpts.DefaultVersion;
        _previousPrependToRoute = Config.VerOpts.PrependToRoute;
        _previousPrefix = Config.VerOpts.Prefix;
        _previousRouteTemplate = Config.VerOpts.RouteTemplate;
        _previousRoutePrefix = Config.EpOpts.RoutePrefix;

        ResetVersioningConfig();
    }

    public void Dispose()
    {
        Config.VerOpts.DefaultVersion = _previousDefaultVersion;
        Config.VerOpts.PrependToRoute = _previousPrependToRoute;
        Config.VerOpts.Prefix = _previousPrefix;
        Config.VerOpts.RouteTemplate = _previousRouteTemplate;
        Config.EpOpts.RoutePrefix = _previousRoutePrefix;
    }

    static void ResetVersioningConfig()
    {
        Config.VerOpts.DefaultVersion = 0;
        Config.VerOpts.PrependToRoute = null;
        Config.VerOpts.Prefix = "v";
        Config.VerOpts.RouteTemplate = null;
        Config.EpOpts.RoutePrefix = null;
    }

    [Fact]
    public void default_version_is_not_prepended_when_ep_version_is_zero()
    {
        ResetVersioningConfig();
        Config.VerOpts.DefaultVersion = 1;
        Config.VerOpts.PrependToRoute = true;

        var route = new StringBuilder().BuildRoute(0, "health", null);

        route.ShouldEndWith("health");
        route.ShouldNotContain("v1");
    }

    [Fact]
    public void default_version_is_not_appended_when_ep_version_is_zero()
    {
        ResetVersioningConfig();
        Config.VerOpts.DefaultVersion = 1;
        Config.VerOpts.PrependToRoute = false;

        var route = new StringBuilder().BuildRoute(0, "health", null);

        route.ShouldEndWith("health");
        route.ShouldNotContain("v1");
    }

    [Fact]
    public void versioned_endpoint_still_gets_prepended_segment()
    {
        ResetVersioningConfig();
        Config.VerOpts.DefaultVersion = 1;
        Config.VerOpts.PrependToRoute = true;

        var route = new StringBuilder().BuildRoute(1, "orders", null);

        route.ShouldContain("v1/orders");
    }

    [Fact]
    public void versioned_endpoint_still_gets_appended_segment()
    {
        ResetVersioningConfig();
        Config.VerOpts.DefaultVersion = 1;
        Config.VerOpts.PrependToRoute = false;

        var route = new StringBuilder().BuildRoute(1, "orders", null);

        route.ShouldEndWith("orders/v1");
    }

    [Fact]
    public void route_template_is_skipped_when_ep_version_is_zero()
    {
        ResetVersioningConfig();
        Config.VerOpts.DefaultVersion = 1;
        Config.VerOpts.RouteTemplate = "{version}";

        var route = new StringBuilder().BuildRoute(0, "health", null);

        route.ShouldEndWith("health");
        route.ShouldNotContain("v1");
        route.ShouldNotContain("{version}");
    }

    [Fact]
    public void route_template_is_substituted_when_ep_version_is_set()
    {
        ResetVersioningConfig();
        Config.VerOpts.DefaultVersion = 1;
        Config.VerOpts.RouteTemplate = "{version}";

        var route = new StringBuilder().BuildRoute(2, "api/{version}/orders", null);

        route.ShouldContain("v2/orders");
        route.ShouldNotContain("{version}");
    }

    [Fact]
    public void init_applies_default_version_when_unset()
    {
        ResetVersioningConfig();
        Config.VerOpts.DefaultVersion = 1;
        var def = new EndpointDefinition(typeof(object), typeof(EmptyRequest), typeof(object));

        def.Version.Init();

        def.Version.Current.ShouldBe(1);
    }

    [Fact]
    public void init_keeps_version_zero_when_dont_version_is_set()
    {
        ResetVersioningConfig();
        Config.VerOpts.DefaultVersion = 1;
        var def = new EndpointDefinition(typeof(object), typeof(EmptyRequest), typeof(object));

        def.DontVersion();
        def.Version.Init();

        def.Version.Current.ShouldBe(0);
    }

    [Fact]
    public void version_after_dont_version_re_enables_versioning()
    {
        ResetVersioningConfig();
        Config.VerOpts.DefaultVersion = 1;
        var def = new EndpointDefinition(typeof(object), typeof(EmptyRequest), typeof(object));

        def.DontVersion();
        def.EndpointVersion(2);
        def.Version.Init();

        def.Version.Current.ShouldBe(2);
        def.Version.SkipDefault.ShouldBeFalse();
    }

    [Fact]
    public void dont_version_after_version_resets_to_zero()
    {
        ResetVersioningConfig();
        Config.VerOpts.DefaultVersion = 1;
        var def = new EndpointDefinition(typeof(object), typeof(EmptyRequest), typeof(object));

        def.EndpointVersion(2, deprecateAt: 4);
        def.DontVersion();
        def.Version.Init();

        def.Version.Current.ShouldBe(0);
        def.Version.StartingReleaseVersion.ShouldBe(0);
        def.Version.DeprecatedAt.ShouldBe(0);
        def.Version.SkipDefault.ShouldBeTrue();
    }
}
