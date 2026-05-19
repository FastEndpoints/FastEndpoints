using System.Runtime.CompilerServices;

namespace TestCases.StreamCommandBusTest;

public record StreamNumbersCommand(int Count) : IStreamCommand<int>;

public sealed class StreamNumbersHandler : IStreamCommandHandler<StreamNumbersCommand, int>
{
    public async IAsyncEnumerable<int> ExecuteAsync(StreamNumbersCommand cmd, [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < cmd.Count; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }
}

public record GenericStreamCommand<TItem>(int Count) : IStreamCommand<TItem> where TItem : new();

public sealed class GenericStreamCommandHandler<TItem> : IStreamCommandHandler<GenericStreamCommand<TItem>, TItem>
    where TItem : new()
{
    public async IAsyncEnumerable<TItem> ExecuteAsync(GenericStreamCommand<TItem> cmd, [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < cmd.Count; i++)
        {
            await Task.Yield();
            yield return new TItem();
        }
    }
}
