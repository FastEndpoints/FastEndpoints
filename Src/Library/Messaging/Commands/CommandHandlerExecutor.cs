namespace FastEndpoints;

interface ICommandHandlerExecutor
{
    Task Execute(ICommand command, Type handlerType, CancellationToken ct);
}

sealed class CommandHandlerExecutor<TCommand> : ICommandHandlerExecutor where TCommand : ICommand
{
    public Task Execute(ICommand command, Type tCommandHandler, CancellationToken ct)
        => ((ICommandHandler<TCommand>)Cfg.ServiceResolver.CreateInstance(tCommandHandler)).ExecuteAsync((TCommand)command, ct);
}

sealed class FakeCommandHandlerExecutor<TCommand>(ICommandHandler<TCommand> handler) : ICommandHandlerExecutor where TCommand : ICommand
{
    public Task Execute(ICommand command, Type tCommandHandler, CancellationToken ct)
        => handler.ExecuteAsync((TCommand)command, ct);
}

interface ICommandHandlerExecutor<TResult>
{
    Task<TResult> Execute(ICommand<TResult> command, Type handlerType, CancellationToken ct);
}

sealed class CommandHandlerExecutor<TCommand, TResult> : ICommandHandlerExecutor<TResult> where TCommand : ICommand<TResult>
{
    public Task<TResult> Execute(ICommand<TResult> command, Type tCommandHandler, CancellationToken ct)
        => ((ICommandHandler<TCommand, TResult>)Cfg.ServiceResolver.CreateInstance(tCommandHandler)).ExecuteAsync((TCommand)command, ct);
}

sealed class FakeCommandHandlerExecutor<TCommand, TResult>(ICommandHandler<TCommand, TResult> handler) : ICommandHandlerExecutor<TResult>
    where TCommand : ICommand<TResult>
{
    public Task<TResult> Execute(ICommand<TResult> command, Type tCommandHandler, CancellationToken ct)
        => handler.ExecuteAsync((TCommand)command, ct);
}