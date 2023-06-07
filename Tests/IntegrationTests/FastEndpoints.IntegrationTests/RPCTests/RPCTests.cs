using IntegrationTests.Shared.Fixtures;
using TestCases.CommandBusTest;
using Xunit.Abstractions;

namespace FastEndpoints.IntegrationTests.CommandBusTests;

public class RPCTests : EndToEndTestBase
{
    public RPCTests(EndToEndTestFixture endToEndTestFixture, ITestOutputHelper outputHelper) : base(endToEndTestFixture, outputHelper)
    {

    }

    //Todo: figure out how to do integration tests for RPC
    //[Fact]
    public async Task RPC_Command_That_Returns_A_Result()
    {
        var res1 = await new TestCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        }
        .RemoteExecuteAsync();

        res1.Should().Be("johnny lawrence");
    }
}