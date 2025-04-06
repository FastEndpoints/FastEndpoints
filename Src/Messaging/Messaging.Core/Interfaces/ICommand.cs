namespace FastEndpoints;

/// <summary>
/// </summary>
public sealed class Void
{
    internal static readonly Void Instance = new();
}

/// <summary>
/// </summary>
public interface IJobResult;

/// <summary>
/// common marker interface for all command types.
/// </summary>
public interface ICommandBase;

/// <summary>
/// interface for a command that does not return anything
/// </summary>
public interface ICommand : ICommand<Void>;

/// <summary>
/// interface for a command that returns a <typeparamref name="TResult" />
/// </summary>
/// <typeparam name="TResult">the type of the result that will be returned from the handler of this command (i.e. <see cref="ICommandHandler{TCommand, TResult}" />)</typeparam>
public interface ICommand<out TResult> : ICommandBase;

/// <summary>
/// </summary>
public interface IHasTrackingID
{
    /// <summary>
    /// tracking id of the job
    /// </summary>
    Guid TrackingID { get; set; }
}

/// <summary>
/// interface for a trackable job that returns a <typeparamref name="TResult" />
/// </summary>
/// <typeparam name="TResult">the type of the result</typeparam>
public interface ITrackableJob<out TResult> : ICommand<TResult>, IHasTrackingID where TResult : IJobResult;

/// <summary>
/// interface for a command that returns a stream of <typeparamref name="TResult" />
/// </summary>
/// <typeparam name="TResult">
/// the type of the result stream that will be returned from the handler of this command (i.e.
/// <see cref="IServerStreamCommandHandler{TCommand, TResult}" />)
/// </typeparam>
public interface IServerStreamCommand<out TResult> where TResult : class;

//Note: client streams are marked by IAsyncEnumerable<T> interface.
//      so we don't need our own interface for client streams.