namespace TestCases.EventHandlingTest;

public class SomeEvent : IEvent
{
    public int One { get; set; }
    public int Two { get; set; }
}

public class Handler1 : IEventHandler<SomeEvent>
{

    public Task HandleAsync(SomeEvent eventModel, CancellationToken ct)
    {
        eventModel.One = 100;
        return Task.CompletedTask;
    }
}

public class Handler2 : IEventHandler<SomeEvent>
{
    public Task HandleAsync(SomeEvent eventModel, CancellationToken ct)
    {
        eventModel.Two = 200;
        return Task.CompletedTask;
    }
}
