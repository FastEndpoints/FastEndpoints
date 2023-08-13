using FastEndpoints;
using Shared;
using TestCases.EventBusTest;
using Xunit;

namespace EventBus;

public class EventBusTests : TestBase
{
    public EventBusTests(WebFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Original_Handler_Execution()
    {
        var (rsp, res) = await Web.GuestClient.GETAsync<Endpoint, int>();

        rsp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        res.Should().Be(200);
    }

    [Fact]
    public async Task Fake_Handler_Execution()
    {
        var client = Web.CreateClient(s =>
        {
            s.RegisterTestEventHandler<TestEvent, FakeEventHandler>();
            s.RegisterTestEventHandler<TestEvent, AnotherFakeEventHandler>();
        });

        var (rsp, res) = await client.GETAsync<Endpoint, int>();

        rsp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        FakeEventHandler.Result.Should().Be(101);
        AnotherFakeEventHandler.Result.Should().Be(102);
    }

    [DontRegister]
    sealed class FakeEventHandler : IEventHandler<TestEvent>
    {
        public static int Result;

        public Task HandleAsync(TestEvent eventModel, CancellationToken ct)
        {
            Result = eventModel.Id + 1;
            return Task.CompletedTask;
        }
    }

    [DontRegister]
    sealed class AnotherFakeEventHandler : IEventHandler<TestEvent>
    {
        public static int Result;

        public Task HandleAsync(TestEvent eventModel, CancellationToken ct)
        {
            Result = eventModel.Id + 2;
            return Task.CompletedTask;
        }
    }
}
