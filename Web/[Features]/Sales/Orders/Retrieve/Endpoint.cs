using Web.PipelineBehaviors.PreProcessors;

namespace Sales.Orders.Retrieve;

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/sales/orders/retrieve/{orderID}");
        PreProcessors(new SecurityProcessor<Request>());
        AllowAnonymous();
        Tags("orders");
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        return SendAsync(Response);
    }
}