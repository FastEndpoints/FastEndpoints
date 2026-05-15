using System.Runtime.CompilerServices;

namespace NativeAotChecker.Endpoints.Commands;

sealed class StreamCommandExecutionRequest
{
    [RouteParam]
    public int Count { get; set; }
}

sealed class StreamCommandExecutionEndpoint : Endpoint<StreamCommandExecutionRequest, IEnumerable<int>>
{
    public override void Configure()
    {
        Get("stream-command/{count}");
        AllowAnonymous();
    }

    public override async Task<IEnumerable<int>> ExecuteAsync(StreamCommandExecutionRequest req, CancellationToken ct)
    {
        var results = new List<int>();

        await foreach (var item in new StreamNumbersAotCommand(req.Count).ExecuteAsync(ct))
            results.Add(item);

        return results;
    }
}

sealed record StreamNumbersAotCommand(int Count) : IStreamCommand<int>;

sealed class StreamNumbersAotCommandHandler : IStreamCommandHandler<StreamNumbersAotCommand, int>
{
    public async IAsyncEnumerable<int> ExecuteAsync(StreamNumbersAotCommand cmd, [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < cmd.Count; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }
}
