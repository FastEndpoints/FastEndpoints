using Microsoft.AspNetCore.Authorization;

namespace NativeAotChecker.Endpoints;

sealed class CommandExecutionRequest
{
    [RouteParam]
    public string Name { get; set; }
}

[HttpGet("command-execution/{name}"), AllowAnonymous]
sealed class CommandExecutionEndpoint : Endpoint<CommandExecutionRequest, string>
{
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
        => Task.FromResult(cmd.Name.Reverse().ToString() ?? string.Empty);
}