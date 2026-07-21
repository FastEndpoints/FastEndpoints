using FastEndpoints;
using FastEndpoints.OpenApi;

namespace OpenApi;

public class GlobalConfigTests : IDisposable
{
    readonly string? _routePrefix = Config.EpOpts.RoutePrefix;

    [Fact]
    public void endpoint_route_prefix_trims_surrounding_slashes()
    {
        Config.EpOpts.RoutePrefix = "/api/";

        GlobalConfig.EndpointRoutePrefix.ShouldBe("api");
    }

    [Fact]
    public void endpoint_route_prefix_treats_empty_string_as_null()
    {
        Config.EpOpts.RoutePrefix = string.Empty;

        GlobalConfig.EndpointRoutePrefix.ShouldBeNull();
    }

    public void Dispose()
    {
        Config.EpOpts.RoutePrefix = _routePrefix;
        GC.SuppressFinalize(this);
    }
}
