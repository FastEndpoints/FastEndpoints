﻿namespace TestCases.EventHandlingTest;

public class NewItemAddedToStock
{
    public int ID { get; set; }
    public string? Name { get; set; }
    public int Quantity { get; set; }
}

public class NotifyCustomers : FastEventHandler<NewItemAddedToStock>
{
    public override Task HandleAsync(NewItemAddedToStock eventModel, CancellationToken ct)
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
