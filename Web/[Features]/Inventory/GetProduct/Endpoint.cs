namespace Inventory.GetProduct
{
    public class Endpoint : EndpointWithoutRequest<Response>
    {
        public override void Configure()
        {
            Verbs(Http.GET);
            Routes("/inventory/get-product/{ProductID}");
            AllowAnonymous();
            ResponseCache(10);
        }

        public override Task HandleAsync(EmptyRequest req, CancellationToken ct)
        {
            Response.ProductID = HttpContext.Request.RouteValues["ProductID"]?.ToString();
            Response.LastModified = DateTime.UtcNow.Ticks;

            return Task.CompletedTask;
        }
    }
}
