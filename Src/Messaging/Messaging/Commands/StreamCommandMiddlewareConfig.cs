using System.Diagnostics.CodeAnalysis;

namespace FastEndpoints;

/// <summary>
/// stream command middleware configuration
/// </summary>
public class StreamCommandMiddlewareConfig
{
    internal List<(Type tInterface, Type tImplementation)> Middleware { get; } = [];

    /// <summary>
    /// register one or more open-generic stream command middleware pieces in the order you'd like them registered.
    /// <code>
    /// c.Register(typeof(StreamCommandLogger&lt;,&gt;), typeof(StreamCommandValidator&lt;,&gt;));
    /// </code>
    /// </summary>
    /// <param name="middlewareTypes">the open-generic middleware types to add to the pipeline.</param>
    /// <exception cref="ArgumentException">thrown if any of the supplied types are not open-generic.</exception>
    [RequiresUnreferencedCode(
        "open-generic middleware registration is not compatible with native aot/trimming. use the generic Register<TCommand, TResult, TMiddleware>() method instead.")]
    public void Register(params Type[] middlewareTypes)
    {
        for (var i = 0; i < middlewareTypes.Length; i++)
        {
            var tMiddleware = middlewareTypes[i];

            if (!IsValid(tMiddleware))
                throw new ArgumentException($"{tMiddleware.Name} must be an open generic type implementing IStreamCommandMiddleware<TCommand, TResult>");

            Middleware.Add((typeof(IStreamCommandMiddleware<,>), tMiddleware));
        }

        static bool IsValid(Type type)
            => type.IsGenericTypeDefinition &&
               type.GetGenericArguments().Length == 2 &&
               type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamCommandMiddleware<,>));
    }

    /// <summary>
    /// register a closed-generic stream command middleware in the pipeline.
    /// </summary>
    /// <typeparam name="TCommand">the type of the command</typeparam>
    /// <typeparam name="TResult">the type of items in the result stream</typeparam>
    /// <typeparam name="TMiddleware">the type of the middleware</typeparam>
    public void Register<TCommand, TResult, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TCommand : IStreamCommand<TResult>
        where TMiddleware : IStreamCommandMiddleware<TCommand, TResult>
        => Middleware.Add((typeof(IStreamCommandMiddleware<TCommand, TResult>), typeof(TMiddleware)));
}
