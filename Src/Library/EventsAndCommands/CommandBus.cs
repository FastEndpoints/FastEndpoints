namespace FastEndpoints;

/// <summary>
/// base class for the command bus
/// </summary>
public abstract class CommandBase
{
    //key: TCommand 
    //val: a concrete command handler instance (subscriber)
    internal static readonly Dictionary<Type, ICommandHandler> handlersDictionary = new();
}

/// <summary>
/// command bus which uses an in-process Request/Response messaging system
/// </summary>
/// <typeparam name="TCommand">the type of notification command dto</typeparam>
/// <typeparam name="TResult">the type of the Response result dto</typeparam>
public class Command<TCommand, TResult> : CommandBase where TCommand : notnull, ICommand<TResult>
{
    private readonly ICommandHandler<TCommand, TResult> _handler = null;

    /// <summary>
    /// instantiates an command facade for the given command dto type.
    /// </summary>
    public Command()
    {
        if (handlersDictionary.TryGetValue(typeof(TCommand), out var handler))
            _handler = handler as ICommandHandler<TCommand, TResult>;
    }

    /// <summary>
    /// send the given model/dto to the registered handler of the command
    /// </summary>
    /// <param name="commandModel">the command model/dto to handle</param>
    ///<param name="cancellation">an optional cancellation token</param>
    /// <returns/>a Task of the response result that matches the command type.
    public Task<TResult> ExecuteAsync(TCommand commandModel, CancellationToken cancellation = default)
    {
        if (_handler == null)
            throw new Exception($"Couldn't find a registered handler for the command of type '{typeof(TCommand).Name}'");

        return _handler.ExecuteAsync(commandModel, cancellation);
    }
}

/// <summary>
/// command bus which uses an in-process Request/Response messaging system
/// </summary>
/// <typeparam name="TCommand">the type of notification command dto</typeparam>
public class Command<TCommand> : CommandBase where TCommand : notnull, ICommand
{
    private readonly ICommandHandler<TCommand> _handler = null;

    /// <summary>
    /// instantiates an command facade for the given command dto type.
    /// </summary>
    public Command()
    {
        if (handlersDictionary.TryGetValue(typeof(TCommand), out var handler))
            _handler = handler as ICommandHandler<TCommand>;
    }

    /// <summary>
    /// send the given model/dto to the registered handler of the command
    /// </summary>
    /// <param name="commandModel">the command model/dto to handle</param>
    ///<param name="cancellation">an optional cancellation token</param>
    /// <returns/>a Task of the response result that matches the command type.
    public Task ExecuteAsync(TCommand commandModel, CancellationToken cancellation = default)
    {
        if (_handler == null)
            throw new Exception($"Couldn't find a registered handler for the command of type '{typeof(TCommand).Name}'");

        return _handler.ExecuteAsync(commandModel, cancellation);
    }
}
