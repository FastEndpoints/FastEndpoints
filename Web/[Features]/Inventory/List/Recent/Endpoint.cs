namespace Inventory.List.Recent;

public class Endpoint : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/inventory/list/recent/{CategoryID}");
    }

    public override Task HandleAsync(EmptyRequest r, CancellationToken t)
    {
        Response.Category = HttpContext.GetRouteValue("CategoryID")?.ToString();
        return SendAsync(Response);
    }
}
