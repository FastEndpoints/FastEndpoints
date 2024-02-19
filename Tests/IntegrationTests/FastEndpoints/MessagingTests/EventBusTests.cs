using TestCases.EventBusTest;
using TestCases.EventHandlingTest;

namespace Messaging;

public class EventBusTests(Fixture f, ITestOutputHelper o) : TestClass<Fixture>(f, o)
{
    [Fact]
    public async Task Fake_Handler_Execution()
    {
        var (rsp, _) = await Fixture.Client.GETAsync<Endpoint, int>();

        rsp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        FakeEventHandler.Result.Should().Be(101);
        AnotherFakeEventHandler.Result.Should().Be(102);
    }

    [Fact]
    public async Task EventHandling()
    {
        var event1 = new NewItemAddedToStock { ID = 1, Name = "one", Quantity = 10 };
        var event2 = new NewItemAddedToStock { ID = 2, Name = "two", Quantity = 20 };
        var event3 = new NewItemAddedToStock { ID = 3, Name = "three", Quantity = 30 };

        await new EventBus<NewItemAddedToStock>().PublishAsync(event1, Mode.WaitForNone);
        await new EventBus<NewItemAddedToStock>().PublishAsync(event2, Mode.WaitForAny);
        await new EventBus<NewItemAddedToStock>().PublishAsync(event3);

        event3.ID.Should().Be(0);
        event3.Name.Should().Be("pass");

        event2.ID.Should().Be(0);
        event2.Name.Should().Be("pass");

        event1.ID.Should().Be(0);
        event1.Name.Should().Be("pass");
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