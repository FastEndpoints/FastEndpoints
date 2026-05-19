namespace TestCases.StreamCommandBusTest;

public class StreamCmdEndpoint : EndpointWithoutRequest<IEnumerable<int>>
{
    public override void Configure()
    {
        Get("/tests/stream-command");
        AllowAnonymous();
    }

    public override async Task<IEnumerable<int>> ExecuteAsync(CancellationToken ct)
    {
        var results = new List<int>();

        await foreach (var item in new StreamNumbersCommand(5).ExecuteAsync(ct))
            results.Add(item);

        return results;
    }
}

public class GenericStreamCmdEndpoint : EndpointWithoutRequest<IEnumerable<Guid>>
{
    public override void Configure()
    {
        Get("/tests/generic-stream-command");
        AllowAnonymous();
    }

    public override async Task<IEnumerable<Guid>> ExecuteAsync(CancellationToken ct)
    {
        var results = new List<Guid>();

        await foreach (var item in new GenericStreamCommand<Guid>(3).ExecuteAsync(ct))
            results.Add(item);

        return results;
    }
}
