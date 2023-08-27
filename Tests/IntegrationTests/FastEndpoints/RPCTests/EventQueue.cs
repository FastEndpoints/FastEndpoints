using FastEndpoints;
using Shared;
using TestCases.EventQueueTest;
using Xunit;

namespace RemoteProcedureCalls;

public class EventQueue : RPCTestBase
{
    public EventQueue(AppFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Event_Queue()
    {
        for (var i = 0; i < 100; i++)
        {
            var evnt = new TestEvent { Id = i };
            evnt.Broadcast();
            await Task.Delay(10);
        }
        TestEventHandler.Received.Count.Should().Be(100);
        TestEventHandler.Received.Select(r => r.Id).Except(Enumerable.Range(0, 100)).Any().Should().BeFalse();
    }
}
