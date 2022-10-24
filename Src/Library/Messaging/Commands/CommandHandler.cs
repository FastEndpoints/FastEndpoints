namespace FastEndpoints;

/// <summary>
/// inherit this base class to handle commands sent using Request/Response pattern
/// <para>WARNING: command handlers are singletons. DO NOT maintain state in them. Use the <c>Resolve*()</c> methods to obtain dependencies.</para>
/// </summary>
/// <typeparam name="TCommand">the type of the command to handle</typeparam>
/// <typeparam name="TResult">the type of the response result</typeparam>
public abstract class FastCommandHandler<TCommand, TResult> : HandlerBase, ICommandHandler<TCommand, TResult>, IServiceResolver where TCommand : ICommand<TResult>
{
    /// <summary>
    /// this method will be called when an command of the specified type is executed.
    /// </summary>
    /// <param name="commandModel">the command model/dto received</param>
    /// <param name="ct">an optional cancellation token</param>
    public abstract Task<TResult> ExecuteAsync(TCommand commandModel, CancellationToken ct);
}

/// <summary>
/// inherit this base class to handle commands sent using Request/Response pattern
/// <para>WARNING: command handlers are singletons. DO NOT maintain state in them. Use the <c>Resolve*()</c> methods to obtain dependencies.</para>
/// </summary>
/// <typeparam name="TCommand">the type of the command to handle</typeparam>
public abstract class FastCommandHandler<TCommand> : HandlerBase, ICommandHandler<TCommand>, IServiceResolver where TCommand : ICommand
{
    /// <summary>
    /// this method will be called when an command of the specified type is published.
    /// </summary>
    /// <param name="commandModel">the command model/dto received</param>
    /// <param name="ct">an optional cancellation token</param>
    public abstract Task ExecuteAsync(TCommand commandModel, CancellationToken ct);
}