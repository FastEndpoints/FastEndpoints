using System.Runtime.CompilerServices;

namespace TestCases.ServerStreamingTest;

public sealed class StatusUpdateHandler : IServerStreamCommandHandler<StatusStreamCommand, StatusUpdate>
{
    public async IAsyncEnumerable<StatusUpdate> ExecuteAsync(StatusStreamCommand command, [EnumeratorCancellation] CancellationToken ct)
    {
        try
        {
            for (var i = 1; !ct.IsCancellationRequested && (command.Count == 0 || i <= command.Count); i++)
            {
                try
                {
                    await Task.Delay(10, ct);
                }
                catch (TaskCanceledException)
                {
                    //do nothing
                }
                yield return new() { Message = $"Id: {command.Id} - {i}" };
            }
        }
        finally
        {
            StreamHandlerTracker.Ended(command.Id.ToString());
        }
    }
}
