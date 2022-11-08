using System.Collections.Concurrent;

namespace FastEndpoints;

public static partial class CommandExtensions
{
    //key: tCommand //val: handler definition
    internal static readonly Dictionary<Type, Type> handlerCache = new();
    private static readonly ConcurrentDictionary<Type, CommandHandlerBase> commandHandlers = new();

    /// <summary>
    /// executes the command that does not return a result
    /// </summary>
    /// <typeparam name="TCommand">the type of the command</typeparam>
    /// <param name="command">the command to execute</param>
    /// <param name="ct">optional cancellation token</param>
    /// <exception cref="InvalidOperationException">thrown when a handler for the command cannot be instantiated</exception>
    public static Task ExecuteAsync<TCommand>(this TCommand command, CancellationToken ct = default) where TCommand : ICommand
    {
        var tCommand = command.GetType();

        if (handlerCache.TryGetValue(tCommand, out var handlerType))
        {
            var handler = Config.ServiceResolver.CreateInstance(handlerType);
            return ((ICommandHandler<TCommand>)handler).ExecuteAsync(command, ct);
        }

        throw new InvalidOperationException($"Unable to create an instance of the handler for command [{tCommand.FullName}]");
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
        if (handlerCache.TryGetValue(tCommand, out var handlerType))
        {
            var handler = (CommandHandlerWrapper<TResult>)commandHandlers.GetOrAdd(tCommand,
                static t => (CommandHandlerWrapper<TResult>)(Activator.CreateInstance(typeof(CommandHandlerWrapperImpl<,>).MakeGenericType(t, typeof(TResult)))
                                                             ?? throw new InvalidOperationException($"Unable to create an instance of the handler for command [{t.FullName}]")));
            return handler.Handle(command, handlerType, ct);
        }

        throw new InvalidOperationException($"Unable to create an instance of the handler for command [{tCommand.FullName}]");
    }
}