namespace FastEndpoints;

//NOTE: CommandHandlerExecutor<> class is singleton
//      (cached in CommandHandlerDefinition.HandlerExecutor property)
interface ICommandHandlerExecutor<TResult>
{
    Task<TResult> Execute(ICommand<TResult> command, Type handlerType, CancellationToken ct);
}

sealed class CommandHandlerExecutor<TCommand, TResult>(IEnumerable<ICommandMiddleware<TCommand, TResult>> m, ICommandReceiver<TCommand>? commandReceiver = null)
    : ICommandHandlerExecutor<TResult> where TCommand : ICommand<TResult>
{
    internal ICommandHandler<TCommand, TResult>? TestHandler { get; init; }

    readonly Type[] _tMiddlewares = m.Select(x => x.GetType()).ToArray();

    public Task<TResult> Execute(ICommand<TResult> command, Type tCommandHandler, CancellationToken ct)
    {
        commandReceiver?.AddCommand((TCommand)command);

        var cmdHandler = TestHandler ?? //TestHandler is not null for unit tests
                         (ICommandHandler<TCommand, TResult>)ServiceResolver.Instance.CreateInstance(tCommandHandler);

        return InvokeMiddleware(0);

        Task<TResult> InvokeMiddleware(int index)
        {
            return index == _tMiddlewares.Length
                       ? cmdHandler.ExecuteAsync((TCommand)command, ct)
                       : ((ICommandMiddleware<TCommand, TResult>)ServiceResolver.Instance.CreateInstance(_tMiddlewares[index])).ExecuteAsync(
                           (TCommand)command,
                           () => InvokeMiddleware(index + 1),
                           ct);
        }
    }
}

//NOTE: StreamCommandHandlerExecutor<> class is singleton
//      (cached in CommandHandlerDefinition.HandlerExecutor property)
interface IStreamCommandHandlerExecutor<TResult>
{
    IAsyncEnumerable<TResult> Execute(IStreamCommand<TResult> command, Type handlerType, CancellationToken ct);
}

sealed class StreamCommandHandlerExecutor<TCommand, TResult>(
    IEnumerable<IStreamCommandMiddleware<TCommand, TResult>> m,
    ICommandReceiver<TCommand>? commandReceiver = null)
    : IStreamCommandHandlerExecutor<TResult> where TCommand : IStreamCommand<TResult>
{
    internal IStreamCommandHandler<TCommand, TResult>? TestHandler { get; init; }

    readonly Type[] _tMiddlewares = m.Select(x => x.GetType()).ToArray();

    public IAsyncEnumerable<TResult> Execute(IStreamCommand<TResult> command, Type tCommandHandler, CancellationToken ct)
    {
        commandReceiver?.AddCommand((TCommand)command);

        var cmdHandler = TestHandler ?? //TestHandler is not null for unit tests
                         (IStreamCommandHandler<TCommand, TResult>)ServiceResolver.Instance.CreateInstance(tCommandHandler);

        return InvokeMiddleware(0);

        IAsyncEnumerable<TResult> InvokeMiddleware(int index)
        {
            return index == _tMiddlewares.Length
                       ? cmdHandler.ExecuteAsync((TCommand)command, ct)
                       : ((IStreamCommandMiddleware<TCommand, TResult>)ServiceResolver.Instance.CreateInstance(_tMiddlewares[index])).ExecuteAsync(
                           (TCommand)command,
                           () => InvokeMiddleware(index + 1),
                           ct);
        }
    }
}
