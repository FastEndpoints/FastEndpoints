namespace FastEndpoints;

public static class CommandExtensions
{
    //key: tCommand //val: handler definition
    internal static readonly Dictionary<Type, CommandHandlerDefinition> handlerCache = new();

    /// <summary>
    /// executes the command that does not return a result
    /// </summary>
    /// <typeparam name="TCommand">the type of the command</typeparam>
    /// <param name="command">the command to execute</param>
    /// <param name="ct">optional cancellation token</param>
    /// <exception cref="InvalidOperationException">thrown when a handler for the command cannot be instantiated</exception>
    public static Task ExecuteAsync(this ICommand command, CancellationToken ct = default)
    {
        var tCommand = command.GetType();

        if (handlerCache.TryGetValue(tCommand, out var def))
        {
            def.HandlerExecutor ??= CreateHandlerExecutor(tCommand);
            return ((CommandHandlerExecutorBase)def.HandlerExecutor).Execute(command, def.HandlerType, ct);
        }

        throw new InvalidOperationException($"Unable to create an instance of the handler for command [{tCommand.FullName}]");

        static CommandHandlerExecutorBase CreateHandlerExecutor(Type tCommand)
            => (CommandHandlerExecutorBase)
                    Config.ServiceResolver.CreateSingleton(
                        Types.CommandHandlerExecutorOf1.MakeGenericType(tCommand));
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
            def.HandlerExecutor ??= CreateHandlerExecutor(tCommand);
            return ((CommandHandlerExecutorBase<TResult>)def.HandlerExecutor).Execute(command, def.HandlerType, ct);
        }

        throw new InvalidOperationException($"Unable to create an instance of the handler for command [{tCommand.FullName}]");

        static CommandHandlerExecutorBase<TResult> CreateHandlerExecutor(Type tCommand)
            => (CommandHandlerExecutorBase<TResult>)
                    Config.ServiceResolver.CreateSingleton(
                        Types.CommandHandlerExecutorOf2.MakeGenericType(tCommand, typeof(TResult)));
    }
}