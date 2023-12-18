namespace FastEndpoints;

abstract class CommandHandlerExecutorBase
{
    internal abstract Task Execute(ICommand command, Type handlerType, CancellationToken ct);
}

sealed class CommandHandlerExecutor<TCommand> : CommandHandlerExecutorBase where TCommand : ICommand
{
    internal override Task Execute(ICommand command, Type tCommandHandler, CancellationToken ct)
        => ((ICommandHandler<TCommand>)Cfg.ServiceResolver.CreateInstance(tCommandHandler)).ExecuteAsync((TCommand)command, ct);
}

sealed class FakeCommandHandlerExecutor<TCommand> : CommandHandlerExecutorBase where TCommand : ICommand
{
    readonly ICommandHandler<TCommand> _handler;

    public FakeCommandHandlerExecutor(ICommandHandler<TCommand> handler)
    {
        _handler = handler;
    }

    internal override Task Execute(ICommand command, Type tCommandHandler, CancellationToken ct)
        => _handler.ExecuteAsync((TCommand)command, ct);
}

abstract class CommandHandlerExecutorBase<TResult>
{
    internal abstract Task<TResult> Execute(ICommand<TResult> command, Type handlerType, CancellationToken ct);
}

sealed class CommandHandlerExecutor<TCommand, TResult> : CommandHandlerExecutorBase<TResult> where TCommand : ICommand<TResult>
{
    internal override Task<TResult> Execute(ICommand<TResult> command, Type tCommandHandler, CancellationToken ct)
        => ((ICommandHandler<TCommand, TResult>)Cfg.ServiceResolver.CreateInstance(tCommandHandler)).ExecuteAsync((TCommand)command, ct);
}

sealed class FakeCommandHandlerExecutor<TCommand, TResult> : CommandHandlerExecutorBase<TResult> where TCommand : ICommand<TResult>
{
    readonly ICommandHandler<TCommand, TResult> _handler;

    public FakeCommandHandlerExecutor(ICommandHandler<TCommand, TResult> handler)
    {
        _handler = handler;
    }

    internal override Task<TResult> Execute(ICommand<TResult> command, Type tCommandHandler, CancellationToken ct)
        => _handler.ExecuteAsync((TCommand)command, ct);
}