using IntegrationTests.Shared.Fixtures;
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
    public async Task RPC_Command_That_Returns_A_Result()
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
    public async Task RPC_Command_That_Returns_The_Same_DTO()
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
    public async Task RPC_Command_That_Returns_A_Server_Stream()
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
}