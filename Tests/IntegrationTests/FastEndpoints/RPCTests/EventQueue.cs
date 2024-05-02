using TestCases.EventQueueTest;

namespace RemoteProcedureCalls;

public class EventQueue(Sut f) : RpcTestBase(f)
{
    [Fact]
    public async Task Event_Queue()
    {
        for (var i = 0; i < 100; i++)
        {
            var evnt = new TestEventQueue { Id = i };
            evnt.Broadcast();
            await Task.Delay(10);
        }

        while (TestEventQueueHandler.Received.Count < 1)
            await Task.Delay(500);

        TestEventQueueHandler.Received.Count.Should().Be(100);
        TestEventQueueHandler.Received.Select(r => r.Id).Except(Enumerable.Range(0, 100)).Any().Should().BeFalse();
    }
}