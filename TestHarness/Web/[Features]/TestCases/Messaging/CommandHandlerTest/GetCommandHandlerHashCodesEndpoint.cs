namespace TestCases.CommandHandlerTest;

sealed class GetCommandHandlerHashCodes : EndpointWithoutRequest<IEnumerable<int>>
{
    public override void Configure()
    {
        Get("tests/command-handler-hashcodes");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        List<int> hashCodes = [];

        // execute 10 commands in parallel
        await Parallel.ForEachAsync(
            Enumerable.Range(1, 10),
            c,
            async (_, ct) =>
            {
                var hc = await new GetCommandHandlerHashCodesCommand().ExecuteAsync(ct);
                hashCodes.Add(hc);
            });

        // return the collected hash codes from handler instances
        await Send.OkAsync(hashCodes, c);
    }
}

sealed class GetCommandHandlerHashCodesCommand : ICommand<int> { }

sealed class GetCommandHandlerHashCodesHandler : ICommandHandler<GetCommandHandlerHashCodesCommand, int>
{
    readonly int _hashCode;

    public GetCommandHandlerHashCodesHandler()
    {
        _hashCode = GetHashCode();
    }

    public Task<int> ExecuteAsync(GetCommandHandlerHashCodesCommand cmd, CancellationToken c)
        => Task.FromResult(_hashCode); // return the hashcode of this handler instance
}