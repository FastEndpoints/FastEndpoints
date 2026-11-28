using System.Runtime.CompilerServices;
using TestCases;
using TestCases.ClientStreamingTest;

namespace RemoteProcedureCalls;

public class ClientStreamCommand(Sut f) : RpcTestBase(f)
{
    [Fact]
    public async Task Client_Stream()
    {
        var input = GetDataStream();

        var report = await Remote.ExecuteClientStream<CurrentPosition, ProgressReport>(input, typeof(IAsyncEnumerable<CurrentPosition>), default);

        report.LastNumber.ShouldBe(5);

        static async IAsyncEnumerable<CurrentPosition> GetDataStream()
        {
            var i = 0;

            while (i < 5)
            {
                i++;

                yield return new() { Number = i };
            }
        }
    }

    [Fact]
    public async Task Client_Stream_Failing_Command_Stream_Cancels_The_Server_Handler()
    {
        var trackingId = Guid.NewGuid().ToString();

        var ex = await Should.ThrowAsync<ApplicationException>(
                     () => FailingStream().RemoteExecuteAsync<CurrentPosition, ProgressReport>(Cancellation));

        ex.Message.ShouldBe("command stream failure");
        await StreamHandlerTracker.WhenEnded(trackingId).WaitAsync(TimeSpan.FromSeconds(5), Cancellation);

        async IAsyncEnumerable<CurrentPosition> FailingStream()
        {
            yield return new() { Number = 1, TrackingId = trackingId };

            //only fail once the server handler is running, so there's a handler that must be cancelled
            await StreamHandlerTracker.WhenStarted(trackingId).WaitAsync(TimeSpan.FromSeconds(5), Cancellation);

            throw new ApplicationException("command stream failure");
        }
    }

    [Fact]
    public async Task Client_Stream_Caller_Cancellation_Reaches_A_Waiting_Command_Stream()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var trackingId = Guid.NewGuid().ToString();

        //fails the test instead of hanging it if the cancellation doesn't reach the command stream
        await Should.ThrowAsync<OperationCanceledException>(
            () => WaitingStream().RemoteExecuteAsync<CurrentPosition, ProgressReport>(cts.Token).WaitAsync(TimeSpan.FromSeconds(10)));

        await StreamHandlerTracker.WhenEnded(trackingId).WaitAsync(TimeSpan.FromSeconds(5), Cancellation);

        async IAsyncEnumerable<CurrentPosition> WaitingStream([EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new() { Number = 1, TrackingId = trackingId };

            await StreamHandlerTracker.WhenStarted(trackingId).WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
            await cts.CancelAsync();

            //waits for the next item until the token passed to the enumerator is cancelled
            await Task.Delay(Timeout.Infinite, ct);

            yield return new() { Number = 2 };
        }
    }
}