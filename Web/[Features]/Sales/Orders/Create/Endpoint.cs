using FastEndpoints;
using Web.PipelineBehaviors.PostProcessors;
using Web.PipelineBehaviors.PreProcessors;

namespace Sales.Orders.Create
{
    public class Endpoint : Endpoint<Request, Response>
    {
        public Endpoint()
        {
            Verbs(Http.POST);
            Routes("/sales/orders/create");
            PreProcessors(
                new MyRequestLogger<Request>());
            PostProcessors(
                new MyResponseLogger<Request, Response>());
        }

        protected override Task HandleAsync(Request r, CancellationToken t)
        {
            return SendAsync(new Response
            {
                Message = "order created!",
                OrderID = 54321
            });
        }
    }
}