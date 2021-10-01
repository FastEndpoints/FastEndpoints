using FastEndpoints;
using Web.SystemEvents;

namespace Shipping.EventHandlers
{
    public class StartOrderProcessing : FastEventHandler<NewOrderCreated>
    {
        public override async Task HandleAsync(NewOrderCreated eventModel, CancellationToken ct)
        {
            //await Task.Delay(1000);
            Console.WriteLine(Environment.NewLine);
            Console.WriteLine($"new order created event received:[{eventModel.OrderID}] and order processing has begun!");
            Console.WriteLine(Environment.NewLine);
            await Task.CompletedTask;
        }
    }
}
