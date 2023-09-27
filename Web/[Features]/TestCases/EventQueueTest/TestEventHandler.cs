namespace TestCases.EventQueueTest;

public class TestEventQueueHandler : IEventHandler<TestEventQueue>
{
    public static List<TestEventQueue> Received { get; } = new();

    public Task HandleAsync(TestEventQueue eventModel, CancellationToken ct)
    {
        Received.Add(eventModel);
        return Task.CompletedTask;
    }
}