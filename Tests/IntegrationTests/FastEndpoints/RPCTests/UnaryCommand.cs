using TestCases.CommandBusTest;

namespace RemoteProcedureCalls;

public class UnaryCommand(Fixture f, ITestOutputHelper o) : RpcTestBase(f, o)
{
    [Fact]
    public async Task Unary()
    {
        var command = new SomeCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };

        var res = await Remote.ExecuteUnary(command, command.GetType(), default);

        res.Should().Be("johnny lawrence");
    }

    [Fact]
    public async Task Unary_Echo()
    {
        var command = new EchoCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };

        var res = await Remote.ExecuteUnary(command, command.GetType(), default);

        res.Should().BeEquivalentTo(command);
    }
}