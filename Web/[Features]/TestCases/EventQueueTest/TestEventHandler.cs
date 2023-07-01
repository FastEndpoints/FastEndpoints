namespace TestCases.EventQueueTest;

public class TestEventHandler : IEventHandler<TestEvent>
{
    public static List<TestEvent> Received { get; } = new();

    public Task HandleAsync(TestEvent eventModel, CancellationToken ct)
    {
        Received.Add(eventModel);
        return Task.CompletedTask;
    }
}