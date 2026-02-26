using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace Sample.Model;

public static class EdmModel
{
    public static IEdmModel Instance { get; } = CreateModel();

    static IEdmModel CreateModel()
    {
        var builder = new ODataConventionModelBuilder();

        builder.EntitySet<Customer>("Customers");
        builder.EntitySet<Order>("Orders");
        builder.ComplexType<Address>();

        return builder.GetEdmModel();
    }
}