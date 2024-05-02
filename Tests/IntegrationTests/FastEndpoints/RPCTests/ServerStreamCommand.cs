using Grpc.Core;
using TestCases.ServerStreamingTest;

namespace RemoteProcedureCalls;

public class ServerStreamCommand(Sut f) : RpcTestBase(f)
{
    [Fact]
    public async Task Server_Stream()
    {
        var command = new StatusStreamCommand
        {
            Id = 101
        };

        var iterator = Remote.ExecuteServerStream(command, command.GetType(), default).ReadAllAsync();

        var i = 1;

        await foreach (var status in iterator)
        {
            status.Message.Should().Be($"Id: {101} - {i}");
            i++;

            if (i == 10)
                break;
        }
    }
}