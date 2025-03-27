using TestCases.EventBusTest;
using TestCases.EventHandlingTest;

namespace Messaging;

public class EventBusTests(Sut App) : TestBase<Sut>
{
    [Fact]
    public async Task Fake_Handler_Execution()
    {
        var (rsp, _) = await App.Client.GETAsync<Endpoint, int>();

        rsp.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        FakeEventHandler.Result.ShouldBe(101);
        AnotherFakeEventHandler.Result.ShouldBe(102);
    }

    [Fact]
    public async Task Test_Event_Receiver_Receives_Event()
    {
        var (rsp, _) = await App.Client.GETAsync<Endpoint, int>();
        rsp.IsSuccessStatusCode.ShouldBeTrue();

        var receiver = App.Services.GetTestEventReceiver<TestEventBus>();
        var res = await receiver.WaitForMatchAsync(e => e.Id == 100, ct: Cancellation);
        res.Any().ShouldBeTrue();
    }

    [Fact]
    public async Task EventHandling()
    {
        var event1 = new NewItemAddedToStock { ID = 1, Name = "one", Quantity = 10 };
        var event2 = new NewItemAddedToStock { ID = 2, Name = "two", Quantity = 20 };
        var event3 = new NewItemAddedToStock { ID = 3, Name = "three", Quantity = 30 };

        await new EventBus<NewItemAddedToStock>().PublishAsync(event1, Mode.WaitForNone, Cancellation);
        await new EventBus<NewItemAddedToStock>().PublishAsync(event2, Mode.WaitForAny, Cancellation);
        await new EventBus<NewItemAddedToStock>().PublishAsync(event3, cancellation: Cancellation);

        event3.ID.ShouldBe(0);
        event3.Name.ShouldBe("pass");

        event2.ID.ShouldBe(0);
        event2.Name.ShouldBe("pass");

        event1.ID.ShouldBe(0);
        event1.Name.ShouldBe("pass");
    }
}

[DontRegister]
sealed class FakeEventHandler : IEventHandler<TestEventBus>
{
    public static int Result;

    public Task HandleAsync(TestEventBus eventModel, CancellationToken ct)
    {
        Result = eventModel.Id + 1;

        return Task.CompletedTask;
    }
}

[DontRegister]
sealed class AnotherFakeEventHandler : IEventHandler<TestEventBus>
{
    public static int Result;

    public Task HandleAsync(TestEventBus eventModel, CancellationToken ct)
    {
        Result = eventModel.Id + 2;

        return Task.CompletedTask;
    }
}