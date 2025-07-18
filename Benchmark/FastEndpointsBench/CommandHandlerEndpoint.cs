﻿using FastEndpoints;

namespace FEBench;

public class Command : ICommand<EmptyResponse> { }

public class CommandHandlerEndpoint1 : Endpoint<Command, EmptyResponse>
{
    public override void Configure()
    {
        Get("command-handler-1");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Command req, CancellationToken ct)
        => await Send.ResponseAsync(new());
}

public class CommandHandlerEndpoint2 : Endpoint<Command, EmptyResponse>
{
    public override void Configure()
    {
        Get("command-handler-2");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Command cmd, CancellationToken ct)
        => await Send.ResponseAsync(await cmd.ExecuteAsync());
}

public class CommandHandler : CommandHandler<Command, EmptyResponse>
{
    public override Task<EmptyResponse> ExecuteAsync(Command command, CancellationToken ct = default)
        => Task.FromResult(new EmptyResponse());
}