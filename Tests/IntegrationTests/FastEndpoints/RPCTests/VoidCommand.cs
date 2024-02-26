namespace RemoteProcedureCalls;

public class VoidCommand(AppFixture f) : RpcTestBase(f)
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