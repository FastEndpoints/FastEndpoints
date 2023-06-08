using IntegrationTests.Shared.Fixtures;
using TestCases.CommandBusTest;
using Xunit;
using Xunit.Abstractions;

namespace FastEndpoints.IntegrationTests.CommandBusTests;

public class RPCTests : EndToEndTestBase
{
    private HttpMessageHandler httpMessageHandler;

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
}