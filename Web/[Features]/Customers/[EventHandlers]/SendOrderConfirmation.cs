using FastEndpoints;
using Web.SystemEvents;

namespace Customers.EventHandlers
{
    public class SendOrderConfirmation : FastEventHandler<NewOrderCreated>
    {
        //todo: add property injection support

        public override Task HandleAsync(NewOrderCreated eventModel, CancellationToken ct)
        {
            Console.WriteLine(Environment.NewLine);
            Console.WriteLine($"new order created event received:[{eventModel.OrderID}] and order confirmation mail sent!");
            Console.WriteLine(Environment.NewLine);
            return Task.CompletedTask;
        }
    }
}
