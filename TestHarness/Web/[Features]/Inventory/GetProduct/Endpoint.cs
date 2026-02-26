namespace Inventory.GetProduct;

public class Endpoint : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/inventory/get-product/{ProductID}");
        AccessControl("Inventory_Retrieve_Item", "Admin");
        AllowAnonymous();
        ResponseCache(10);
        Summary(x => x.ResponseParam<Response>(r => r.LastModified, "blah blah blah"));
    }

    public override Task<Response> ExecuteAsync(CancellationToken ct)
        => Task.FromResult(
            new Response
            {
                LastModified = DateTime.UtcNow.Ticks,
                ProductID = Route<string>("ProductID")
            });
}