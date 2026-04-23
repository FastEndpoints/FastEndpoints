using System.Text;
using Xunit;

namespace FastEndpoints;

public class RoutePrefixTests
{
    [Fact]
    public void empty_global_prefix_is_ignored()
    {
        Config.EpOpts.RoutePrefix = string.Empty;

        var route = new StringBuilder().BuildRoute(0, "orders/create", null);

        route.ShouldBe("orders/create");
    }

    [Fact]
    public void slash_wrapped_global_prefix_is_normalized()
    {
        Config.EpOpts.RoutePrefix = "/api/";

        var route = new StringBuilder().BuildRoute(0, "orders/create", null);

        route.ShouldBe("/api/orders/create");
    }

    [Fact]
    public void endpoint_prefix_override_is_ignored_without_global_prefix()
    {
        Config.EpOpts.RoutePrefix = null;

        var route = new StringBuilder().BuildRoute(0, "orders/create", "mobile/api");

        route.ShouldBe("orders/create");
    }

    [Fact]
    public void empty_endpoint_override_disables_global_prefix()
    {
        Config.EpOpts.RoutePrefix = "api";

        var route = new StringBuilder().BuildRoute(0, "orders/create", string.Empty);

        route.ShouldBe("orders/create");
    }

    [Fact]
    public void slash_wrapped_endpoint_override_is_normalized()
    {
        Config.EpOpts.RoutePrefix = "api";

        var route = new StringBuilder().BuildRoute(0, "orders/create", "/mobile/api/");

        route.ShouldBe("/mobile/api/orders/create");
    }
}
