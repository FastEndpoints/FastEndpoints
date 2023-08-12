using FastEndpoints;
using Shared;
using TestCases.CommandBusTest;
using Xunit;

namespace CommandBus;

public class CommandBusTests : TestBase
{
    public CommandBusTests(WebFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CommandHandlerSendsErrorResponse()
    {
        var res = await Web.GuestClient.GETAsync<TestCases.CommandHandlerTest.Endpoint, ErrorResponse>();
        res.Response.IsSuccessStatusCode.Should().BeFalse();
        res.Result!.StatusCode.Should().Be(400);
        res.Result.Errors.Count.Should().Be(2);
        res.Result.Errors["GeneralErrors"].Count.Should().Be(2);
    }

    [Fact]
    public async Task CommandThatReturnsAResult()
    {
        var res1 = await new TestCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        }
        .ExecuteAsync();

        var res2 = await new TestCommand
        {
            FirstName = "jo",
            LastName = "law"
        }
        .ExecuteAsync();

        res1.Should().Be("johnny lawrence");
        res2.Should().Be("jo law");
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

    [Fact]
    public async Task NonConcreteCommandThatReturnsVoid()
    {
        ICommand cmd = new TestVoidCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };

        var act = async () => cmd.ExecuteAsync();

        await act.Should().NotThrowAsync();
    }
}