using FastEndpoints.Messaging.Remote.Testing;
using TestCases.EventQueueTest;

namespace RemoteProcedureCalls;

public class EventQueue(Sut App) : RpcTestBase(App)
{
    readonly Sut _app = App;

    [Fact]
    public async Task Event_Queue()
    {
        for (var i = 0; i < 100; i++)
        {
            var evnt = new TestEventQueue { Id = i };
            evnt.Broadcast();
            await Task.Delay(10, Cancellation);
        }

        while (TestEventQueueHandler.Received.Count < 1)
            await Task.Delay(500, Cancellation);

        TestEventQueueHandler.Received.Count.ShouldBe(100);
        TestEventQueueHandler.Received.Select(r => r.Id).Except(Enumerable.Range(0, 100)).Any().ShouldBeFalse();
    }

    [Fact]
    public async Task Event_Queue_TestEventReceiver()
    {
        var evnt = new MyEvent { Name = "blah blah" };
        evnt.Broadcast();

        var receiver = _app.Services.GetTestEventReceiver<MyEvent>();
        var received = await receiver.WaitForMatchAsync(e => e.Name == "blah blah", ct: Cancellation);
        received.Any().ShouldBeTrue();
    }
}