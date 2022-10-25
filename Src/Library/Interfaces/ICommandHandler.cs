namespace FastEndpoints;

public interface ICommandHandler { }

public interface ICommandHandler<in TCommand> : ICommandHandler where TCommand : ICommand
{
    Task ExecuteAsync(TCommand command, CancellationToken ct);
}

public interface ICommandHandler<in TCommand, TResult> : ICommandHandler where TCommand : ICommand<TResult>
{
    Task<TResult> ExecuteAsync(TCommand command, CancellationToken ct);
}
