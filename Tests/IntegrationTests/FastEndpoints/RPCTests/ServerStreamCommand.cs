using Grpc.Core;
using TestCases;
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

        var iterator = Remote.ExecuteServerStream(command, command.GetType(), default).WithCancellation(Cancellation);

        var i = 1;

        await foreach (var status in iterator)
        {
            status.Message.ShouldBe($"Id: {101} - {i}");
            i++;

            if (i == 10)
                break;
        }
    }

    [Fact]
    public async Task Server_Stream_Reads_A_Finite_Stream_To_The_End()
    {
        var command = new StatusStreamCommand { Id = 205, Count = 3 };
        var messages = new List<string>();

        await Within(
            async () =>
            {
                await foreach (var status in command.RemoteExecuteAsync(Cancellation))
                    messages.Add(status.Message);
            });

        messages.ShouldBe(["Id: 205 - 1", "Id: 205 - 2", "Id: 205 - 3"]);
        await HandlerEnded(command.Id);
    }

    [Fact]
    public async Task Server_Stream_Each_Enumeration_Starts_A_New_Call()
    {
        var results = new StatusStreamCommand { Id = 206, Count = 2 }.RemoteExecuteAsync(Cancellation);

        await Within(
            async () =>
            {
                for (var run = 0; run < 2; run++)
                {
                    var messages = new List<string>();

                    await foreach (var status in results)
                        messages.Add(status.Message);

                    messages.ShouldBe(["Id: 206 - 1", "Id: 206 - 2"]);
                }
            });
    }

    [Fact]
    public async Task Server_Stream_Breaking_Early_Cancels_The_Server_Handler()
    {
        var command = new StatusStreamCommand { Id = 201 };
        var count = 0;

        await foreach (var _ in command.RemoteExecuteAsync(Cancellation))
        {
            if (++count == 3)
                break;
        }

        await HandlerEnded(command.Id);
    }

    [Fact]
    public async Task Server_Stream_Exception_In_Caller_Loop_Cancels_The_Server_Handler()
    {
        var command = new StatusStreamCommand { Id = 202 };

        var ex = await Should.ThrowAsync<ApplicationException>(
                     async () =>
                     {
                         await foreach (var _ in command.RemoteExecuteAsync(Cancellation))
                             throw new ApplicationException("caller failure");
                     });

        ex.Message.ShouldBe("caller failure");
        await HandlerEnded(command.Id);
    }

    [Fact]
    public async Task Server_Stream_Caller_Cancellation_Ends_The_Call()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var command = new StatusStreamCommand { Id = 203 };

        var ex = await Should.ThrowAsync<RpcException>(
                     () => Within(
                         async () =>
                         {
                             await foreach (var _ in command.RemoteExecuteAsync(cts.Token))
                                 await cts.CancelAsync();
                         }));

        ex.StatusCode.ShouldBe(StatusCode.Cancelled);
        await HandlerEnded(command.Id);
    }

    [Fact]
    public async Task Server_Stream_Enumerator_Cancellation_Ends_The_Call()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var command = new StatusStreamCommand { Id = 204 };

        var ex = await Should.ThrowAsync<RpcException>(
                     () => Within(
                         async () =>
                         {
                             await foreach (var _ in command.RemoteExecuteAsync(Cancellation).WithCancellation(cts.Token))
                                 await cts.CancelAsync();
                         }));

        ex.StatusCode.ShouldBe(StatusCode.Cancelled);
        await HandlerEnded(command.Id);
    }

    // fails the test instead of hanging it if the call doesn't end
    static Task Within(Func<Task> body)
        => body().WaitAsync(TimeSpan.FromSeconds(10));

    // fails the test instead of hanging it if the server handler keeps running
    Task HandlerEnded(int commandId)
        => StreamHandlerTracker.WhenEnded(commandId.ToString()).WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
}