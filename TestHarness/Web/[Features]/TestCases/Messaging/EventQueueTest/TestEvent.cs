namespace TestCases.EventQueueTest;

public class TestEventQueue : IEvent
{
    public int Id { get; set; }
}

sealed class MyEvent : IEvent
{
    public string Name { get; set; }
}