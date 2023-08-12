using Shared;
using TestCases.CommandBusTest;
using Xunit;

namespace RemoteProcedureCalls;

public class VoidCommand : RPCTestBase
{
    public VoidCommand(WebFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Void()
    {
        var command = new TestVoidCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };
        await remote.ExecuteVoid(command, command.GetType(), default);
    }
}
