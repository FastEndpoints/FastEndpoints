using Web.SystemEvents;

namespace Shipping.EventHandlers;

public class StartOrderProcessing : FastEventHandler<NewOrderCreated>
{
    public async override Task HandleAsync(NewOrderCreated eventModel, CancellationToken ct)
    {
        var logger = Resolve<ILogger<StartOrderProcessing>>();

        logger?.LogWarning($"new order created event received:[{eventModel.OrderID}] and order processing has begun!");

        await Task.CompletedTask;
    }
}