namespace FastEndpoints;

/// <summary>
/// </summary>
public sealed class VoidResult { }

/// <summary>
/// common marker interface for all command types.
/// </summary>
public interface ICommandBase { }

/// <summary>
/// interface for a command that does not return anything
/// </summary>
public interface ICommand : ICommand<VoidResult> { }

/// <summary>
/// interface for a command that returns a <typeparamref name="TResult" />
/// </summary>
/// <typeparam name="TResult">the type of the result that will be returned from the handler of this command (i.e. <see cref="ICommandHandler{TCommand, TResult}" />)</typeparam>
public interface ICommand<out TResult> : ICommandBase { }

/// <summary>
/// interface for a command that returns a stream of <typeparamref name="TResult" />
/// </summary>
/// <typeparam name="TResult">
/// the type of the result stream that will be returned from the handler of this command (i.e.
/// <see cref="IServerStreamCommandHandler{TCommand, TResult}" />)
/// </typeparam>
public interface IServerStreamCommand<out TResult> where TResult : class { }

//Note: client streams are marked by IAsyncEnumerable<T> interface.
//      so we don't need our own interface for client streams.