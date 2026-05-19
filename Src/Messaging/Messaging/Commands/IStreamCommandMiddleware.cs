namespace FastEndpoints;

/// <summary>
/// interface for creating a stream command middleware used to build a pipeline around stream command handlers.
/// </summary>
/// <typeparam name="TCommand">the type of the command</typeparam>
/// <typeparam name="TResult">the type of items in the result stream</typeparam>
public interface IStreamCommandMiddleware<in TCommand, TResult> where TCommand : IStreamCommand<TResult>
{
    /// <summary>
    /// implement this method to run some common piece of logic for all stream command handlers.
    /// make sure to execute the <paramref name="next" /> delegate within your logic in order to not break the pipeline.
    /// </summary>
    /// <param name="command">the command instance</param>
    /// <param name="next">the stream command delegate to execute next</param>
    /// <param name="ct">cancellation token</param>
#pragma warning disable CS8424
    IAsyncEnumerable<TResult> ExecuteAsync(TCommand command, StreamCommandDelegate<TResult> next, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct);
#pragma warning restore CS8424
}

/// <summary>
/// stream command delegate
/// </summary>
/// <typeparam name="TResult">the type of items in the result stream</typeparam>
public delegate IAsyncEnumerable<TResult> StreamCommandDelegate<TResult>();
