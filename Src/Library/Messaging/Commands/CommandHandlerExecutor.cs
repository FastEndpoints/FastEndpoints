namespace FastEndpoints;

//NOTE: CommandHandlerExecutor<> class is singleton
//      (cached in CommandHandlerDefinition.HandlerExecutor property)

interface ICommandHandlerExecutor<TResult>
{
    Task<TResult> Execute(ICommand<TResult> command, Type handlerType, CancellationToken ct);
}

sealed class CommandHandlerExecutor<TCommand, TResult>(IEnumerable<ICommandMiddleware<TCommand, TResult>> m,
                                                       ICommandHandler<TCommand, TResult>? handler = null)
    : ICommandHandlerExecutor<TResult> where TCommand : ICommand<TResult>
{
    readonly ICommandMiddleware<TCommand, TResult>[] _middlewares = m.ToArray();

    public Task<TResult> Execute(ICommand<TResult> command, Type tCommandHandler, CancellationToken ct)
    {
        var cmdHandler = handler ?? //handler is not null for unit tests
                         (ICommandHandler<TCommand, TResult>)Cfg.ServiceResolver.CreateInstance(tCommandHandler);

        return InvokeMiddleware(0);

        Task<TResult> InvokeMiddleware(int index)
        {
            return index == _middlewares.Length
                       ? cmdHandler.ExecuteAsync((TCommand)command, ct)
                       : _middlewares[index].ExecuteAsync((TCommand)command, () => InvokeMiddleware(index + 1), ct);
        }
    }
}