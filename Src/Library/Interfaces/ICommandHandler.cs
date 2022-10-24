namespace FastEndpoints;

internal interface ICommandHandler
{
}

internal interface ICommandHandler<in TCommand, TResult> : ICommandHandler where TCommand : ICommand<TResult>
{
    Task<TResult> ExecuteAsync(TCommand command, CancellationToken ct);
}

internal interface ICommandHandler<in TCommand> : ICommandHandler where TCommand : ICommand
{
    Task ExecuteAsync(TCommand command, CancellationToken ct);
}

public interface ICommand<out TResult>
{
}

public interface ICommand : ICommand<int> { }