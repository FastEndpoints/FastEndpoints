namespace Inventory.GetProduct;

public class Endpoint : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/inventory/get-product/{ProductID}");
        AllowAnonymous();
        ResponseCache(10);
    }

    public override Task<Response> ExecuteAsync(CancellationToken ct)
    {
        return Task.FromResult(new Response()
        {
            LastModified = DateTime.UtcNow.Ticks,
            ProductID = Route<string>("ProductID")
        });
    }
}
