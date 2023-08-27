using Shared;
using TestCases.CommandBusTest;
using Xunit;

namespace RemoteProcedureCalls;

public class VoidCommand : RPCTestBase
{
    public VoidCommand(AppFixture fixture) : base(fixture) { }

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
