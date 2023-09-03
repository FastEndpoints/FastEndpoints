namespace RemoteProcedureCalls;

public class VoidCommand : RPCTestBase
{
    public VoidCommand(Fixture f, ITestOutputHelper o) : base(f, o) { }

    [Fact]
    public async Task Void()
    {
        var command = new TestCases.CommandBusTest.VoidCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };
        await remote.ExecuteVoid(command, command.GetType(), default);
    }
}
