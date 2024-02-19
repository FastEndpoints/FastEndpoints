namespace TestCases.EventHandlingTest;
public class NewItemAddedToStock : IEvent
{
    public int ID { get; set; }
    public string? Name { get; set; }
    public int Quantity { get; set; }
}

public class NotifyCustomers : IEventHandler<NewItemAddedToStock>
{
    public NotifyCustomers(ILogger<NotifyCustomers> logger)
    {
        logger.LogInformation("scope factory and resolve works in IEventHandler!");
    }

    public Task HandleAsync(NewItemAddedToStock eventModel, CancellationToken ct)
    {
        if (eventModel.Quantity == 0)
            throw new ArgumentOutOfRangeException(nameof(eventModel.Quantity), "quantity can't be zero");

        eventModel.ID = 0;
        return Task.CompletedTask;
    }
}

public class UpdateInventoryLevel : FastEventHandler<NewItemAddedToStock>
{
    public override Task HandleAsync(NewItemAddedToStock eventModel, CancellationToken ct)
    {
        eventModel.Name = "pass";
        return Task.CompletedTask;
    }
}
