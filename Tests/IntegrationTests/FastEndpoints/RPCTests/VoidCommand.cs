namespace RemoteProcedureCalls;

public class VoidCommand(Fixture f, ITestOutputHelper o) : RpcTestBase(f, o)
{
    [Fact]
    public async Task Void()
    {
        var command = new TestCases.CommandBusTest.VoidCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };
        await Remote.ExecuteVoid(command, command.GetType(), default);
    }
}