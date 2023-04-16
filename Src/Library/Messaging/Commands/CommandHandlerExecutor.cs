namespace FastEndpoints;

internal abstract class CommandHandlerExecutorBase
{
    internal abstract Task Execute(ICommand command, Type handlerType, CancellationToken ct);
}

internal sealed class CommandHandlerExecutor<TCommand> : CommandHandlerExecutorBase where TCommand : ICommand
{
    internal override Task Execute(ICommand command, Type tCommandHandler, CancellationToken ct)
    {
        return ((ICommandHandler<TCommand>)Config.ServiceResolver.CreateInstance(tCommandHandler))
            .ExecuteAsync((TCommand)command, ct);
    }
}

internal sealed class FakeCommandHandlerExecutor<TCommand> : CommandHandlerExecutorBase where TCommand : ICommand
{
    private readonly ICommandHandler<TCommand> _handler;

    public FakeCommandHandlerExecutor(ICommandHandler<TCommand> handler)
    {
        _handler = handler;
    }

    internal override Task Execute(ICommand command, Type tCommandHandler, CancellationToken ct)
    {
        return _handler.ExecuteAsync((TCommand)command, ct);
    }
}

internal abstract class CommandHandlerExecutorBase<TResult>
{
    internal abstract Task<TResult> Execute(ICommand<TResult> command, Type handlerType, CancellationToken ct);
}

internal sealed class CommandHandlerExecutor<TCommand, TResult> : CommandHandlerExecutorBase<TResult> where TCommand : ICommand<TResult>
{
    internal override Task<TResult> Execute(ICommand<TResult> command, Type tCommandHandler, CancellationToken ct)
    {
        return ((ICommandHandler<TCommand, TResult>)Config.ServiceResolver.CreateInstance(tCommandHandler))
            .ExecuteAsync((TCommand)command, ct);
    }
}

internal sealed class FakeCommandHandlerExecutor<TCommand, TResult> : CommandHandlerExecutorBase<TResult> where TCommand : ICommand<TResult>
{
    private readonly ICommandHandler<TCommand, TResult> _handler;

    public FakeCommandHandlerExecutor(ICommandHandler<TCommand, TResult> handler)
    {
        _handler = handler;
    }

    internal override Task<TResult> Execute(ICommand<TResult> command, Type tCommandHandler, CancellationToken ct)
    {
        return _handler.ExecuteAsync((TCommand)command, ct);
    }
}