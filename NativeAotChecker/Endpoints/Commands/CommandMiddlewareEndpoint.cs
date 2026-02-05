namespace NativeAotChecker.Endpoints.Commands;

sealed class CommandMiddlewareRequest
{
    public string Input { get; set; } = "";
}

sealed class CommandMiddlewareResponse
{
    public string Result { get; set; } = "";
}

sealed class CommandMiddlewareEndpoint : Endpoint<CommandMiddlewareRequest, CommandMiddlewareResponse>
{
    public override void Configure()
    {
        Post("command-middleware");
        AllowAnonymous();
    }

    public override async Task<CommandMiddlewareResponse> ExecuteAsync(CommandMiddlewareRequest req, CancellationToken ct)
    {
        var cmd = new MiddlewareTestCmd { Input = req.Input };
        var result = await cmd.ExecuteAsync(ct);

        return new() { Result = result.Output };
    }
}

class MiddlewareTestCmd : ICommand<MiddlewareTestResult>
{
    public string Input { get; set; } = "";
}

class MiddlewareTestResult
{
    public string Output { get; set; } = "";
}

sealed class MiddlewareTestCmdHandler : ICommandHandler<MiddlewareTestCmd, MiddlewareTestResult>
{
    public Task<MiddlewareTestResult> ExecuteAsync(MiddlewareTestCmd cmd, CancellationToken ct)
        => Task.FromResult(new MiddlewareTestResult { Output = $"{cmd.Input}[handler] << " });
}

sealed class FirstMiddleware : ICommandMiddleware<MiddlewareTestCmd, MiddlewareTestResult>
{
    public async Task<MiddlewareTestResult> ExecuteAsync(MiddlewareTestCmd command, CommandDelegate<MiddlewareTestResult> next, CancellationToken ct)
    {
        command.Input = "[ first-in >> ";
        var result = await next();
        result.Output += "<< first-out ]";

        return result;
    }
}

sealed class SecondMiddleware<TCommand, TResult> : ICommandMiddleware<TCommand, TResult>
    where TCommand : MiddlewareTestCmd, ICommand<TResult>
    where TResult : MiddlewareTestResult
{
    public async Task<TResult> ExecuteAsync(TCommand command, CommandDelegate<TResult> next, CancellationToken ct)
    {
        command.Input += "second-in >> ";
        var result = await next();
        result.Output += "second-out ";

        return result;
    }
}

sealed class ThirdMiddleware<TCommand, TResult> : ICommandMiddleware<TCommand, TResult>
    where TCommand : MiddlewareTestCmd, ICommand<TResult>
    where TResult : MiddlewareTestResult
{
    public async Task<TResult> ExecuteAsync(TCommand command, CommandDelegate<TResult> next, CancellationToken ct)
    {
        command.Input += "third-in >> ";
        var result = await next();
        result.Output += "third-out << ";

        return result;
    }
}