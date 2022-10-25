namespace FastEndpoints;

/// <summary>
/// inherit this base class to handle commands [<see cref="ICommand{TResult}"/>] issued by the command bus pattern.
/// </summary>
/// <typeparam name="TCommand">the type of the command to handle</typeparam>
/// <typeparam name="TResult">the type of the result</typeparam>
public abstract class FastCommandHandler<TCommand, TResult> : HandlerBase, ICommandHandler<TCommand, TResult>, IServiceResolver where TCommand : ICommand<TResult>
{
    /// <summary>
    /// this method will be called when a command of the specified type is executed.
    /// </summary>
    /// <param name="cmd">the command model/dto received</param>
    /// <param name="ct">an optional cancellation token</param>
    public abstract Task<TResult> ExecuteAsync(TCommand cmd, CancellationToken ct = default);
}

/// <summary>
/// inherit this base class to handle commands [<see cref="ICommand{TResult}"/>] issued by the command bus pattern.
/// </summary>
/// <typeparam name="TCommand">the type of the command to handle</typeparam>
public abstract class FastCommandHandler<TCommand> : HandlerBase, ICommandHandler<TCommand>, IServiceResolver where TCommand : ICommand
{
    /// <summary>
    /// this method will be called when a command of the specified type is published.
    /// </summary>
    /// <param name="cmd">the command model/dto received</param>
    /// <param name="ct">an optional cancellation token</param>
    public abstract Task ExecuteAsync(TCommand cmd, CancellationToken ct = default);
}