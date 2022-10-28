using System.Linq.Expressions;
using System.Reflection;

namespace FastEndpoints;

public static class CommandExtensions
{
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

    private static Func<object, CancellationToken, Task<TResult>> HandlerExecutor<TResult>(this Type tHandler, Type tCommand, object handler)
    {
        //Task<TResult> ExecuteAsync((TCommand)cmd, ct);

        var instance = Expression.Constant(handler);
        var execMethod = tHandler.GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)!;
        var cmdParam = Expression.Parameter(Types.Object, "cmd");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");
        var methodCall = Expression.Call(instance, execMethod, Expression.Convert(cmdParam, tCommand), ctParam);

        return Expression.Lambda<Func<object, CancellationToken, Task<TResult>>>(
            methodCall,
            cmdParam,
            ctParam
        ).Compile();
    }

    private static Func<object, CancellationToken, Task> HandlerExecutor(this Type tHandler, Type tCommand, object handler)
    {
        //Task ExecuteAsync((TCommand)cmd, ct);

        var instance = Expression.Constant(handler);
        var execMethod = tHandler.GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)!;
        var cmdParam = Expression.Parameter(Types.Object, "cmd");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");
        var methodCall = Expression.Call(instance, execMethod, Expression.Convert(cmdParam, tCommand), ctParam);

        return Expression.Lambda<Func<object, CancellationToken, Task>>(
            methodCall,
            cmdParam,
            ctParam
        ).Compile();
    }

    internal class CommandHandlerDefinition
    {
        internal Type HandlerType { get; set; }
        internal object? ExecuteMethod { get; set; }

        internal CommandHandlerDefinition(Type handlerType)
        {
            HandlerType = handlerType;
        }
    }
}