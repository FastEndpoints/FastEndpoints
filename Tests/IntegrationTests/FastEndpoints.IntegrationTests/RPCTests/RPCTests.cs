using IntegrationTests.Shared.Fixtures;
using TestCases.ClientStreamingTest;
using TestCases.CommandBusTest;
using TestCases.ServerStreamingTest;
using Xunit;
using Xunit.Abstractions;

namespace FastEndpoints.IntegrationTests.CommandBusTests;

public class RPCTests : EndToEndTestBase
{
    private readonly HttpMessageHandler httpMessageHandler;

    public RPCTests(EndToEndTestFixture endToEndTestFixture, ITestOutputHelper outputHelper) : base(endToEndTestFixture, outputHelper)
    {
        httpMessageHandler = endToEndTestFixture.CreateHttpMessageHandler();
    }

    [Fact]
    public async Task Void_RPC()
    {
        await new TestVoidCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        }
        .TestRemoteExecuteAsync<TestVoidCommand>(httpMessageHandler);
    }

    [Fact]
    public async Task Unary_RPC()
    {
        var res1 = await new TestCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        }
        .TestRemoteExecuteAsync<TestCommand, string>(httpMessageHandler);

        res1.Should().Be("johnny lawrence");
    }

    [Fact]
    public async Task Unary_RPC_Echo()
    {
        var cmd = new EchoCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };

        var res1 = await cmd.TestRemoteExecuteAsync<EchoCommand, EchoCommand>(httpMessageHandler);

        res1.Should().BeEquivalentTo(cmd);
    }

    [Fact]
    public async Task Server_Stream_RPC()
    {
        var iterator = new StatusStreamCommand
        {
            Id = 101
        }.TestRemoteExecuteAsync<StatusStreamCommand, StatusUpdate>(httpMessageHandler);

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
        var report = await GetDataStream()
            .TestRemoteExecuteAsync<CurrentPosition, ProgressReport>(httpMessageHandler);

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