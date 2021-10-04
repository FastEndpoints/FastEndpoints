using FastEndpoints;

namespace Inventory.List.Recent
{
    public class Endpoint : EndpointWithoutRequest<Response>
    {
        public Endpoint()
        {
            Verbs(Http.GET);
            Routes("/inventory/list/recent/{CategoryID}");
        }

        protected override Task HandleAsync(EmptyRequest r, CancellationToken t)
        {
            Response.Category = HttpContext.GetRouteValue("CategoryID")?.ToString();
            return SendAsync(Response);
        }
    }
}