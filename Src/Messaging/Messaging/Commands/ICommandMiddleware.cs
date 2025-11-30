namespace FastEndpoints;

/// <summary>
/// interface for creating a command middleware used to build a pipeline around command handlers.
/// </summary>
/// <typeparam name="TCommand">the type of the command</typeparam>
/// <typeparam name="TResult">the type of the result</typeparam>
public interface ICommandMiddleware<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    /// <summary>
    /// implement this method to run some common piece of logic for all command handlers.
    /// make sure to execute the <paramref name="next" /> delegate within your logic in order to not break the pipeline.
    /// </summary>
    /// <param name="command">the command instance</param>
    /// <param name="next">the command delegate to execute next</param>
    /// <param name="ct">cancellation token</param>
    Task<TResult> ExecuteAsync(TCommand command, CommandDelegate<TResult> next, CancellationToken ct);
}

/// <summary>
/// command delegate
/// </summary>
/// <typeparam name="TResult">the type of the result</typeparam>
public delegate Task<TResult> CommandDelegate<TResult>();
