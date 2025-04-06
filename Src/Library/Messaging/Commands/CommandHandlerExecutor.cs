namespace FastEndpoints;

//NOTE: CommandHandlerExecutor<> classes are singletons
//      (cached in CommandHandlerDefinition.HandlerExecutor property)

interface ICommandHandlerExecutor
{
    Task Execute(ICommand command, Type handlerType, CancellationToken ct);
}

sealed class CommandHandlerExecutor<TCommand>(IEnumerable<ICommandMiddleware<TCommand, VoidResult>> middleware, ICommandHandler<TCommand>? handler = null)
    : ICommandHandlerExecutor where TCommand : ICommand
{
    readonly ICommandMiddleware<TCommand, VoidResult>[] _middlewares = middleware.ToArray();

    public async Task Execute(ICommand command, Type tCommandHandler, CancellationToken ct)
    {
        //handler is not null for unit tests
        var cmdHandler = handler ?? (ICommandHandler<TCommand>)Cfg.ServiceResolver.CreateInstance(tCommandHandler);
        await InvokeMiddleware(0);

        async Task<VoidResult> InvokeMiddleware(int index)
        {
            if (index != _middlewares.Length)
                return await _middlewares[index].ExecuteAsync((TCommand)command, () => InvokeMiddleware(index + 1), ct);

            await cmdHandler.ExecuteAsync((TCommand)command, ct);

            return VoidResult.Instance;
        }
    }
}

interface ICommandHandlerExecutor<TResult>
{
    Task<TResult> Execute(ICommand<TResult> command, Type handlerType, CancellationToken ct);
}

sealed class CommandHandlerExecutor<TCommand, TResult>(IEnumerable<ICommandMiddleware<TCommand, TResult>> middleware,
                                                       ICommandHandler<TCommand, TResult>? handler = null)
    : ICommandHandlerExecutor<TResult> where TCommand : ICommand<TResult>
{
    readonly ICommandMiddleware<TCommand, TResult>[] _middlewares = middleware.ToArray();

    public Task<TResult> Execute(ICommand<TResult> command, Type tCommandHandler, CancellationToken ct)
    {
        //handler is not null for unit tests
        var cmdHandler = handler ?? (ICommandHandler<TCommand, TResult>)Cfg.ServiceResolver.CreateInstance(tCommandHandler);

        return InvokeMiddleware(0);

        Task<TResult> InvokeMiddleware(int index)
        {
            return index == _middlewares.Length
                       ? cmdHandler.ExecuteAsync((TCommand)command, ct)
                       : _middlewares[index].ExecuteAsync((TCommand)command, () => InvokeMiddleware(index + 1), ct);
        }
    }
}