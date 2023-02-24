using FastEndpoints;

namespace FEBench;

public class Command : ICommand<EmptyResponse> { }

public class CommandHandlerEndpoint1 : Endpoint<Command, EmptyResponse>
{
    public override void Configure()
    {
        Get("command-handler-1");
        AllowAnonymous();
    }

    public async override Task HandleAsync(Command req, CancellationToken ct)
        => await SendAsync(new EmptyResponse());
}

public class CommandHandlerEndpoint2 : Endpoint<Command, EmptyResponse>
{
    public override void Configure()
    {
        Get("command-handler-2");
        AllowAnonymous();
    }

    public async override Task HandleAsync(Command cmd, CancellationToken ct)
        => await SendAsync(await cmd.ExecuteAsync());
}

public class CommandHandler : CommandHandler<Command, EmptyResponse>
{
    public override Task<EmptyResponse> ExecuteAsync(Command command, CancellationToken ct = default)
        => Task.FromResult(new EmptyResponse());
}