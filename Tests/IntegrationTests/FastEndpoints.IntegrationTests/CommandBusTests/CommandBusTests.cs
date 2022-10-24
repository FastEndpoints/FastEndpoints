using FastEndpoints.Extensions;
using IntegrationTests.Shared.Fixtures;
using TestCases.CommandBusTest;
using Xunit;
using Xunit.Abstractions;

namespace FastEndpoints.IntegrationTests.CommandBusTests;

public class CommandBusTests : EndToEndTestBase
{
    public CommandBusTests(EndToEndTestFixture endToEndTestFixture, ITestOutputHelper outputHelper) : base(endToEndTestFixture, outputHelper)
    {
        endToEndTestFixture.RegisterTestServices(services => { });
    }

    [Fact]
    public async Task CommandGetsHandled()
    {
        var res = await new TestCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        }
        .ExecuteAsync();

        res.Should().Be("johnny lawrence");
    }
}