using Web.SystemEvents;

namespace Customers.EventHandlers;

public class SendOrderConfirmation(ILogger<SendOrderConfirmation> logger) : IEventHandler<NewOrderCreated>
{
    public Task HandleAsync(NewOrderCreated eventModel, CancellationToken ct)
    {
        logger.LogWarning($"new order created event received:[{eventModel.OrderID}] and order confirmation mail sent!");

        return Task.CompletedTask;
    }
}