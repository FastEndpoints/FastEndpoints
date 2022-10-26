namespace FastEndpoints;

public static class CommandExtensions
{
    internal static readonly Dictionary<Type, HandlerDefinition> handlerCache = new();

    internal class HandlerDefinition
    {
        internal Type HandlerType { get; set; }
        internal object? ExecuteMethod { get; set; }

        internal HandlerDefinition(Type handlerType)
        {
            HandlerType = handlerType;
        }
    }

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
            var handler = Config.ServiceResolver.CreateInstance(def.HandlerType);
            def.ExecuteMethod ??= def.HandlerType.HandlerExecutor<TResult>(tCommand, handler);
            return ((Func<object, CancellationToken, Task<TResult>>)def.ExecuteMethod)(command, ct);
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
            var handler = Config.ServiceResolver.CreateInstance(def.HandlerType);
            def.ExecuteMethod ??= def.HandlerType.HandlerExecutor(tCommand, handler);
            return ((Func<object, CancellationToken, Task>)def.ExecuteMethod)(command, ct);
        }
        throw new InvalidOperationException($"Unable to create an instance of the handler for command [{tCommand.FullName}]");
    }
}