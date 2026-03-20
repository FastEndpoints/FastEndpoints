//using FastEndpoints.Messaging.Remote.Testing;

using TestCases.EventQueueTest;

namespace RemoteProcedureCalls;

public class EventQueue(Sut App) : RpcTestBase(App)
{
    readonly Sut _app = App;
    const int WarmupEventId = -1;

    [Fact]
    public async Task Event_Queue()
    {
        TestEventQueueHandler.Reset();
        new TestEventQueue { Id = WarmupEventId }.Broadcast();
        await WaitUntil(() => TestEventQueueHandler.Received.Any(r => r.Id == WarmupEventId), timeoutMs: 10000);
        TestEventQueueHandler.Reset();

        for (var i = 0; i < 100; i++)
        {
            var evnt = new TestEventQueue { Id = i };
            evnt.Broadcast();
            await Task.Delay(10, Cancellation);
        }

        await WaitUntil(() => TestEventQueueHandler.Received.Count == 100, timeoutMs: 10000);

        TestEventQueueHandler.Received.Count.ShouldBe(100);
        TestEventQueueHandler.Received.Select(r => r.Id).Except(Enumerable.Range(0, 100)).Any().ShouldBeFalse();
    }

    static async Task WaitUntil(Func<bool> condition, int timeoutMs)
    {
        var timeoutAt = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < timeoutAt)
        {
            if (condition())
                return;

            await Task.Delay(50);
        }

        condition().ShouldBeTrue();
    }

    //[Fact]
    // public async Task Event_Queue_TestEventReceiver()
    // {
    //     var evnt = new MyEvent { Name = "blah blah" };
    //     evnt.Broadcast();
    //
    //     var receiver = _app.Services.GetTestEventReceiver<MyEvent>();
    //     var received = await receiver.WaitForMatchAsync(e => e.Name == "blah blah", ct: Cancellation);
    //     received.Any().ShouldBeTrue();
    // }
}
