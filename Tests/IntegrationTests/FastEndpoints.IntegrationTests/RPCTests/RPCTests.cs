using Grpc.Core;
using IntegrationTests.Shared.Fixtures;
using TestCases.ClientStreamingTest;
using TestCases.CommandBusTest;
using TestCases.ServerStreamingTest;
using Xunit;
using Xunit.Abstractions;

namespace FastEndpoints.IntegrationTests.CommandBusTests;

public class RPCTests : EndToEndTestBase
{
    private readonly RemoteConnection remote;

    public RPCTests(EndToEndTestFixture endToEndTestFixture, ITestOutputHelper outputHelper) : base(endToEndTestFixture, outputHelper)
    {
        remote = new RemoteConnection("http://testhost");
        remote.ChannelOptions.HttpHandler = endToEndTestFixture.CreateHttpMessageHandler();
        remote.Register<TestVoidCommand>();
        remote.Register<TestCommand, string>();
        remote.Register<EchoCommand, EchoCommand>();
        remote.RegisterServerStream<StatusStreamCommand, StatusUpdate>();
        remote.RegisterClientStream<CurrentPosition, ProgressReport>();
    }

    [Fact]
    public async Task Void_RPC()
    {
        var command = new TestVoidCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };
        await remote.ExecuteVoid(command, command.GetType(), default);
    }

    [Fact]
    public async Task Unary_RPC()
    {
        var command = new TestCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };

        var res = await remote.ExecuteUnary(command, command.GetType(), default);

        res.Should().Be("johnny lawrence");
    }

    [Fact]
    public async Task Unary_RPC_Echo()
    {
        var command = new EchoCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };

        var res = await remote.ExecuteUnary(command, command.GetType(), default);

        res.Should().BeEquivalentTo(command);
    }

    [Fact]
    public async Task Server_Stream_RPC()
    {
        var command = new StatusStreamCommand
        {
            Id = 101
        };

        var iterator = remote.ExecuteServerStream(command, command.GetType(), default).ReadAllAsync();

        var i = 1;
        await foreach (var status in iterator)
        {
            status.Message.Should().Be($"Id: {101} - {i}");
            i++;
            if (i == 10)
                break;
        }
    }

    [Fact]
    public async Task Client_Stream_RPC()
    {
        var input = GetDataStream();

        var report = await remote.ExecuteClientStream<CurrentPosition, ProgressReport>(
            input, typeof(IAsyncEnumerable<CurrentPosition>), default);

        report.LastNumber.Should().Be(5);

        static async IAsyncEnumerable<CurrentPosition> GetDataStream()
        {
            var i = 0;
            while (i < 5)
            {
                i++;
                yield return new() { Number = i };
            }
        }
    }
}