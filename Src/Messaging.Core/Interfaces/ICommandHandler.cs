using System.Runtime.CompilerServices;

namespace FastEndpoints;

/// <summary>
/// marker interface for all command handlers
/// </summary>
public interface ICommandHandler { }

/// <summary>
/// interface to be implemented by a command handler for a given command type that does not return a result
/// </summary>
/// <typeparam name="TCommand">the type of the command</typeparam>
public interface ICommandHandler<in TCommand> : ICommandHandler where TCommand : ICommand
{
    /// <summary>
    /// accepts a command and does not return a result.
    /// </summary>
    /// <param name="command">the input command object</param>
    /// <param name="ct">optional cancellation token</param>
    Task ExecuteAsync(TCommand command, CancellationToken ct = default);
}

/// <summary>
/// interface to be implemented by a command handler for a given command type that returns a result
/// </summary>
/// <typeparam name="TCommand">the type of the input command</typeparam>
/// <typeparam name="TResult">the type of the result returned</typeparam>
public interface ICommandHandler<in TCommand, TResult> : ICommandHandler where TCommand : ICommand<TResult>
{
    /// <summary>
    /// accepts a command and returns a result.
    /// </summary>
    /// <param name="command">the input command object</param>
    /// <param name="ct">optional cancellation token</param>
    Task<TResult> ExecuteAsync(TCommand command, CancellationToken ct = default);
}

/// <summary>
/// interface to be implemented by a command handler for a given command type that returns <typeparamref name="TResult"/> stream
/// </summary>
/// <typeparam name="TCommand">the type of the input command</typeparam>
/// <typeparam name="TResult">the type of the result stream returned</typeparam>
public interface IServerStreamCommandHandler<in TCommand, TResult> where TCommand : IServerStreamCommand<TResult>
{
#pragma warning disable CS8424
    /// <summary>
    /// accepts a command and returns a <see cref="IAsyncEnumerable{TResult}"/>.
    /// </summary>
    /// <param name="command">the input command object</param>
    /// <param name="ct">optional cancellation token</param>
    IAsyncEnumerable<TResult> ExecuteAsync(TCommand command, [EnumeratorCancellation] CancellationToken ct);
#pragma warning restore CS8424
}