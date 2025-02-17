using Web.SystemEvents;

namespace Shipping.EventHandlers;

public class StartOrderProcessing(ILogger<StartOrderProcessing> logger) : IEventHandler<NewOrderCreated>
{
    public async Task HandleAsync(NewOrderCreated eventModel, CancellationToken ct)
    {
        logger.LogWarning($"new order created event received:[{eventModel.OrderID}] and order processing has begun!");

        await Task.CompletedTask;
    }
}