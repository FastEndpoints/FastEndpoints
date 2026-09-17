namespace NativeAotChecker.Endpoints.Commands;

sealed class VoidCommandMiddlewareRequest
{
    public string Input { get; set; } = "";
}

sealed class VoidCommandMiddlewareResponse
{
    public string Result { get; set; } = "";
}

sealed class VoidCommandMiddlewareEndpoint : Endpoint<VoidCommandMiddlewareRequest, VoidCommandMiddlewareResponse>
{
    public override void Configure()
    {
        Post("void-command-middleware");
        AllowAnonymous();
    }

    public override async Task<VoidCommandMiddlewareResponse> ExecuteAsync(VoidCommandMiddlewareRequest req, CancellationToken ct)
    {
        var cmd = new VoidMiddlewareCmd { Log = req.Input };
        await cmd.ExecuteAsync(ct);

        return new() { Result = cmd.Log };
    }
}

class VoidMiddlewareCmd : ICommand
{
    public string Log { get; set; } = "";
}

sealed class VoidMiddlewareCmdHandler : ICommandHandler<VoidMiddlewareCmd>
{
    public Task ExecuteAsync(VoidMiddlewareCmd command, CancellationToken ct)
    {
        command.Log += "[handler]";

        return Task.CompletedTask;
    }
}

sealed class VoidFirstMiddleware : ICommandMiddleware<VoidMiddlewareCmd, FastEndpoints.Void>
{
    public async Task<FastEndpoints.Void> ExecuteAsync(VoidMiddlewareCmd command, CommandDelegate<FastEndpoints.Void> next, CancellationToken ct)
    {
        command.Log += "first-in>";
        var result = await next();
        command.Log += "<first-out";

        return result;
    }
}

sealed class VoidSecondMiddleware : ICommandMiddleware<VoidMiddlewareCmd, FastEndpoints.Void>
{
    public async Task<FastEndpoints.Void> ExecuteAsync(VoidMiddlewareCmd command, CommandDelegate<FastEndpoints.Void> next, CancellationToken ct)
    {
        command.Log += "second-in>";
        var result = await next();
        command.Log += "<second-out";

        return result;
    }
}
