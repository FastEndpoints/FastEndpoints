namespace FastEndpoints;

/// <summary>
/// the base class from which all <see cref="CommandHandler{TCommand}"/> classes inherit from
/// </summary>
/// <typeparam name="TCommand">the type of the command</typeparam>
public abstract class CommandHandlerBase<TCommand> : ValidationContext<TCommand> { }

/// <summary>
/// inherit this base class if you'd like to manipulate validation state of the calling endpoint from within the command handler.
/// </summary>
/// <typeparam name="TCommand">the type of the command that will be handled by this command handler</typeparam>
public abstract class CommandHandler<TCommand> : CommandHandlerBase<TCommand>, ICommandHandler<TCommand> where TCommand : ICommand
{
    ///<inheritdoc/>
    public abstract Task ExecuteAsync(TCommand command, CancellationToken ct = default);
}

/// <summary>
/// inherit this base class if you'd like to manipulate validation state of the calling endpoint from within the command handler.
/// </summary>
/// <typeparam name="TCommand">the type of the command that will be handled by this command handler</typeparam>
/// <typeparam name="TResult">the type of the result that will be returned by this command handler</typeparam>
public abstract class CommandHandler<TCommand, TResult> : CommandHandlerBase<TCommand>, ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
    ///<inheritdoc/>
    public abstract Task<TResult> ExecuteAsync(TCommand command, CancellationToken ct = default);
}