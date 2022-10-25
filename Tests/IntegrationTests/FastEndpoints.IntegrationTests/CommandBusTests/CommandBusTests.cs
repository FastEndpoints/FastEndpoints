using IntegrationTests.Shared.Fixtures;
using TestCases.CommandBusTest;
using Xunit;
using Xunit.Abstractions;

namespace FastEndpoints.IntegrationTests.CommandBusTests;

public class CommandBusTests : EndToEndTestBase
{
    public CommandBusTests(EndToEndTestFixture endToEndTestFixture, ITestOutputHelper outputHelper) : base(endToEndTestFixture, outputHelper)
    {
    }

    [Fact]
    public async Task CommandThatReturnsAResult()
    {
        var res = await new TestCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        }
        .ExecuteAsync();

        res.Should().Be("johnny lawrence");
    }

    [Fact]
    public async Task CommandThatReturnsVoid()
    {
        var cmd = new TestVoidCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };

        await cmd.ExecuteAsync();

        cmd.FirstName.Should().Be("pass");
        cmd.LastName.Should().Be("pass");
    }
}