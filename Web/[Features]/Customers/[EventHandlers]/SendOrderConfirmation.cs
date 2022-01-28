using Web.SystemEvents;

namespace Customers.EventHandlers;

public class SendOrderConfirmation : FastEventHandler<NewOrderCreated>
{
    public override Task HandleAsync(NewOrderCreated eventModel, CancellationToken ct)
    {
        var logger = Resolve<ILogger<SendOrderConfirmation>>();

        logger?.LogWarning($"new order created event received:[{eventModel.OrderID}] and order confirmation mail sent!");

        return Task.CompletedTask;
    }
}