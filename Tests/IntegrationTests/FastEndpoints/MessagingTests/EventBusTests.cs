using TestCases.EventBusTest;

namespace Messaging;

public class EventBusTests : TestClass<Fixture>
{
    public EventBusTests(Fixture f, ITestOutputHelper o) : base(f, o) { }

    [Fact]
    public async Task Fake_Handler_Execution()
    {
        var (rsp, _) = await Fixture.Client.GETAsync<Endpoint, int>();

        rsp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        FakeEventHandler.Result.Should().Be(101);
        AnotherFakeEventHandler.Result.Should().Be(102);
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