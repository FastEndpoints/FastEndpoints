using Web.PipelineBehaviors.PreProcessors;

namespace Sales.Orders.Retrieve;

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/sales/orders/retrieve/{OrderID}");
        PreProcessors(new SecurityProcessor<Request>());
        AllowAnonymous();
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        return SendAsync(Response);
    }
}