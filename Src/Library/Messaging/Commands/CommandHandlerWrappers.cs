namespace FastEndpoints;

internal abstract class CommandHandlerBase
{
    internal abstract Task<object?> Handle(object request, Type handlerType, CancellationToken cancellationToken);

}

internal abstract class CommandHandlerWrapper<TResult> : CommandHandlerBase
{
    internal abstract Task<TResult> Handle(ICommand<TResult> command, Type handlerType, CancellationToken cancellationToken);
}


internal class CommandHandlerWrapperImpl<TCommand, TResult> : CommandHandlerWrapper<TResult>
    where TCommand : ICommand<TResult>
{
    internal override Task<TResult> Handle(ICommand<TResult> command, Type handlerType, CancellationToken cancellationToken)
    {
        Task<TResult> Handler() => ((ICommandHandler<TCommand, TResult>)Config.ServiceResolver.CreateInstance(handlerType)).ExecuteAsync((TCommand)command, cancellationToken);
        return Handler();
    }

    internal async override Task<object?> Handle(object command, Type handlerType, CancellationToken cancellationToken) =>
        await Handle((ICommand<TResult>)command, handlerType, cancellationToken).ConfigureAwait(false);
}