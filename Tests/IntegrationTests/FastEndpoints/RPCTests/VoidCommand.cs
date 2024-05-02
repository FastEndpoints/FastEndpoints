namespace RemoteProcedureCalls;

public class VoidCommand(Sut f) : RpcTestBase(f)
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