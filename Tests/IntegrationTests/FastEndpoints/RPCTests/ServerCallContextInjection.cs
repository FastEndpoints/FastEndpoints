using Grpc.Core;
using TestCases.CommandBusTest;

namespace RemoteProcedureCalls;

public class ServerCallContextInjection(Sut f) : RpcTestBase(f)
{
    [Fact]
    public async Task Void_Handler_Sees_Context_During_Execution()
    {
        var command = new CallContextVoidCommand { Name = "test" };

        await Remote.ExecuteVoid(command, command.GetType(), default); //handler throws if context is missing
    }

    [Fact]
    public async Task Unary_Handler_Sees_Context_During_Execution()
    {
        var command = new CallContextUnaryCommand { HeaderName = "x-call-context-test" };
        var opts = new CallOptions(new() { { "x-call-context-test", "hello" } }, cancellationToken: Cancellation);

        var res = await Remote.ExecuteUnary(command, command.GetType(), opts);

        res.ShouldBe("hello");
    }
}
