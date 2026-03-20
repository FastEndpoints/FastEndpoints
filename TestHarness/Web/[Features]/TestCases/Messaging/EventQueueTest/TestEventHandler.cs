using System.Collections.Concurrent;

namespace TestCases.EventQueueTest;

public class TestEventQueueHandler : IEventHandler<TestEventQueue>
{
    public static ConcurrentQueue<TestEventQueue> Received { get; } = new();

    public static void Reset()
    {
        while (Received.TryDequeue(out _)) { }
    }

    public Task HandleAsync(TestEventQueue eventModel, CancellationToken ct)
    {
        Received.Enqueue(eventModel);

        return Task.CompletedTask;
    }
}

sealed class MyEventHandler : IEventHandler<MyEvent>
{
    public Task HandleAsync(MyEvent eventModel, CancellationToken ct)
        => Task.CompletedTask;
}
