using Xunit;

namespace FastEndpoints;

public class GroupTests
{
    [Fact]
    public void root_group_prefixes_each_route_and_runs_its_action()
    {
        var def = Definition("invoice", "orders");

        new AdminGroup().Action(def);

        def.Routes.ShouldBe(["admin/invoice", "admin/orders"]);
        def.EndpointTags.ShouldBe(["admin"]);
    }

    [Fact]
    public void subgroup_applies_child_prefix_and_action_before_ancestors()
    {
        var def = Definition("invoice");

        new ReportsGroup().Action(def);

        def.Routes.ShouldBe(["admin/sales/reports/invoice"]);
        def.EndpointTags.ShouldBe(["reports", "sales", "admin"]);
    }

    static EndpointDefinition Definition(params string[] routes)
        => new(typeof(GroupTests), typeof(object), typeof(object)) { Routes = routes };

    public class AdminGroup : Group
    {
        public AdminGroup()
            => Configure("admin", ep => ep.Tags("admin"));
    }

    public class SalesGroup : SubGroup<AdminGroup>
    {
        public SalesGroup()
            => Configure("sales", ep => ep.Tags("sales"));
    }

    public class ReportsGroup : SubGroup<SalesGroup>
    {
        public ReportsGroup()
            => Configure("reports", ep => ep.Tags("reports"));
    }
}
