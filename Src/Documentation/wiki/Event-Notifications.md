# in-process pub/sub notifications

if you'd like to take an event driven approach to building your application, you have the option to publish events from your endpoint handlers and have completely decoupled **event-handlers** to take action when events are published. it's a simple 3 step process to do event driven work.

## 1. define an event model/ dto
this is the data contract that will be communicated across processes.
```csharp
public class OrderCreatedEvent
{
    public string OrderID { get; set; }
    public string CustomerName { get; set; }
    public decimal OrderTotal { get; set; }
}
```

## 2. define an event handler
this is the code that will be fired/executed when events of the above dto type gets published.
```csharp
public class OrderCreationHandler : FastEventHandler<OrderCreatedEvent>
{
    public override Task HandleAsync(OrderCreatedEvent eventModel, CancellationToken ct)
    {
        var logger = Resolve<ILogger<OrderCreationHandler>>();
        logger.LogInformation($"order created event received:[{eventModel.OrderID}]");
        return Task.CompletedTask;
    }
}
```

## 3. publish the event
simply hand in an event model/dto to the `PublishAsync()` method.
```csharp
public class CreateOrderEndpoint : Endpoint<CreateOrderRequest>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/sales/orders/create");
    }

    public override async Task HandleAsync(CreateOrderRequest req, CancellationToken ct)
    {
        var orderID = await orderRepo.CreateNewOrder(req);

        await PublishAsync(new OrderCreatedEvent
        {
            OrderID = orderID,
            CustomerName = req.Customer,
            OrderTotal = req.OrderValue
        });

        await SendOkAsync();
    }
}
```

# the PublishAsync() method

the `PublishAsync()` method has an overload that will take a `Mode` enum that lets you specify whether to wait for **all subscribers** to finish; wait for **any subscriber** to finish; or wait for **none of the subscribers** to finish.

for example, you can publish an event in a fire-n-forget manner with the following:

```csharp
await PublishAsync(eventModel, Mode.WaitForNone);
```

the default mode is `Mode.WaitForAll` which will await all subscribers. i.e. execution will only continue after each and every subscriber of the event has completed their work.

## publishing from event handlers
it is also possible to publish events from within event handlers themselves like so:
```csharp
public class OrderCreationHandler : FastEventHandler<OrderCreatedEvent>
{
    public override async Task HandleAsync(OrderCreatedEvent eventModel, CancellationToken ct)
    {
        await PublishAsync(new ReOrderLevelReachedEvent
        {
            ItemId = "ITM-0001",
            CurrentLevel = 5,
        });
    }
}
```