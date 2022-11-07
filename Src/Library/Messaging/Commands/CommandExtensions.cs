namespace FastEndpoints;

public static partial class CommandExtensions
{
    //key: tCommand //val: handler definition
    internal static readonly Dictionary<Type, CommandHandlerDefinition> handlerCache = new();

    /// <summary>
    /// executes the command and returns a result
    /// </summary>
    /// <typeparam name="TResult">the type of the returned result</typeparam>
    /// <param name="command">the command to execute</param>
    /// <param name="ct">optional cancellation token</param>
    /// <exception cref="InvalidOperationException">thrown when a handler for the command cannot be instantiated</exception>
    public static Task<TResult> ExecuteAsync<TResult>(this ICommand<TResult> command, CancellationToken ct = default)
    {
        var tCommand = command.GetType();

        if (handlerCache.TryGetValue(tCommand, out var def))
        {
            //todo: figure out how to replace methodinfo.invoke() with a compiled expression
            var handler = Config.ServiceResolver.CreateInstance(def.HandlerType);
            return (Task<TResult>)def.ExecuteMethod.Invoke(handler, new object[] { command, ct })!;
        }
        throw new InvalidOperationException($"Unable to create an instance of the handler for command [{tCommand.FullName}]");
    }

    /// <summary>
    /// executes the command that does not return a result
    /// </summary>
    /// <param name="command">the command to execute</param>
    /// <param name="ct">optional cancellation token</param>
    /// <exception cref="InvalidOperationException">thrown when a handler for the command cannot be instantiated</exception>
    public static Task ExecuteAsync(this ICommand command, CancellationToken ct = default)
    {
        var tCommand = command.GetType();

        if (handlerCache.TryGetValue(tCommand, out var def))
        {
            //todo: figure out how to replace methodinfo.invoke() with a compiled expression
            var handler = Config.ServiceResolver.CreateInstance(def.HandlerType);
            return (Task)def.ExecuteMethod.Invoke(handler, new object[] { command, ct })!;
        }
        throw new InvalidOperationException($"Unable to create an instance of the handler for command [{tCommand.FullName}]");
    }
}