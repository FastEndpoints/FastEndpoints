using System.Runtime.CompilerServices;

namespace TestCases.ServerStreamingTest;

public sealed class StatusUpdateHandler : IServerStreamCommandHandler<StatusStreamCommand, StatusUpdate>
{
    public async IAsyncEnumerable<StatusUpdate> ExecuteAsync(StatusStreamCommand command, [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 1; !ct.IsCancellationRequested; i++)
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
}
