namespace FastEndpoints;

internal abstract class CommandHandlerExecutorBase<TResult>
{
    internal abstract Task<TResult> Execute(ICommand<TResult> command, Type handlerType, CancellationToken ct);
}

internal class CommandHandlerExecutor<TCommand, TResult> : CommandHandlerExecutorBase<TResult> where TCommand : ICommand<TResult>
{
    internal override Task<TResult> Execute(ICommand<TResult> command, Type tCommandHandler, CancellationToken ct)
    {
        return ((ICommandHandler<TCommand, TResult>)Config.ServiceResolver.CreateInstance(tCommandHandler))
            .ExecuteAsync((TCommand)command, ct);
    }
}