namespace NativeAotChecker.Endpoints.Commands;

sealed class CommandExecutionRequest
{
    [RouteParam]
    public string Name { get; set; }
}

sealed class CommandExecutionEndpoint : Endpoint<CommandExecutionRequest, string>
{
    public override void Configure()
    {
        Get("command-execution/{name}");
        AllowAnonymous();
    }

    public override Task<string> ExecuteAsync(CommandExecutionRequest req, CancellationToken ct)
    {
        var cmd = new NameReverseCommand { Name = req.Name };

        return cmd.ExecuteAsync(ct);
    }
}

sealed class NameReverseCommand : ICommand<string>
{
    public string Name { get; set; }
}

sealed class NameReverseCommandHandler : ICommandHandler<NameReverseCommand, string>
{
    public Task<string> ExecuteAsync(NameReverseCommand cmd, CancellationToken c)
        => Task.FromResult(string.Concat(cmd.Name.Reverse()));
}