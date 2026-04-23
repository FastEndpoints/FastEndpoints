using FastEndpoints;
using FastEndpoints.Swagger;
using Xunit;

namespace Swagger;

public class GlobalConfigTests
{
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
}