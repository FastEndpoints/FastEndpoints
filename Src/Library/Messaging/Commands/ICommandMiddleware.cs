namespace FastEndpoints;

/// <summary>
/// interface for creating a command middleware used to build a pipeline around command handlers.
/// </summary>
/// <typeparam name="TCommand">the type of the command</typeparam>
/// <typeparam name="TResult">the type of the result</typeparam>
public interface ICommandMiddleware<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    /// <summary>
    /// implement this method to run some common piece of logic for all command handlers.
    /// make sure to execute the <paramref name="next" /> delegate within your logic in order to no break the pipeline.
    /// </summary>
    /// <param name="command">the command instance</param>
    /// <param name="next">the command delegate to execute next</param>
    /// <param name="ct">cancellation token</param>
    Task<TResult> ExecuteAsync(TCommand command, CommandDelegate<TResult> next, CancellationToken ct);
}

/// <summary>
/// command delegate
/// </summary>
/// <typeparam name="TResult">the type of the result</typeparam>
public delegate Task<TResult> CommandDelegate<TResult>();

/// <summary>
/// command middleware configuration
/// </summary>
public class CommandMiddlewareConfig
{
    internal List<(Type tInterface, Type tImplementation)> Middleware { get; } = [];

    /// <summary>
    /// register one or more open-generic command middleware pieces in the order you'd like them registered.
    /// <code>
    /// c.Register(typeof(CommandLogger&lt;,&gt;), typeof(CommandValidator&lt;,&gt;));
    /// </code>
    /// </summary>
    /// <param name="middlewareTypes">the open-generic middleware types to add to the pipeline.</param>
    /// <exception cref="ArgumentException">thrown if any of the supplied types are not open-generic.</exception>
    public void Register(params Type[] middlewareTypes)
    {
        for (var i = 0; i < middlewareTypes.Length; i++)
        {
            var tMiddleware = middlewareTypes[i];

            if (!IsValid(tMiddleware))
                throw new ArgumentException($"{tMiddleware.Name} must be an open generic type implementing ICommandMiddleware<TRequest, TResult>");

            Middleware.Add((typeof(ICommandMiddleware<,>), tMiddleware));
        }

        static bool IsValid(Type type)
            => type.IsGenericTypeDefinition &&
               type.GetGenericArguments().Length == 2 &&
               type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommandMiddleware<,>));
    }

    /// <summary>
    /// register a closed-generic command middleware in the pipeline.
    /// </summary>
    /// <typeparam name="TCommand">the type of the command</typeparam>
    /// <typeparam name="TResult">the type of the result</typeparam>
    /// <typeparam name="TMiddleware">the type of the middleware</typeparam>
    public void Register<TCommand, TResult, TMiddleware>() where TCommand : ICommand<TResult> where TMiddleware : ICommandMiddleware<TCommand, TResult>
        => Middleware.Add((typeof(ICommandMiddleware<TCommand, TResult>), typeof(TMiddleware)));
}