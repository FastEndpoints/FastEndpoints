using System.Diagnostics.CodeAnalysis;

namespace FastEndpoints;

public abstract class CommandMiddlewareConfigBase
{
    internal List<(Type tInterface, Type tImplementation)> Middleware { get; } = [];

    protected abstract Type OpenGenericInterface { get; }

    [RequiresUnreferencedCode(
        "open-generic middleware registration is not compatible with native aot/trimming. use the generic Register<TCommand, TResult, TMiddleware>() method instead.")]
    public void Register(params Type[] middlewareTypes)
    {
        for (var i = 0; i < middlewareTypes.Length; i++)
        {
            var tMiddleware = middlewareTypes[i];

            if (!IsValid(tMiddleware))
                throw new ArgumentException($"{tMiddleware.Name} must be an open generic type implementing {OpenGenericInterface.Name}");

            Middleware.Add((OpenGenericInterface, tMiddleware));
        }
    }

    bool IsValid(Type type)
        => type.IsGenericTypeDefinition &&
           type.GetGenericArguments().Length == 2 &&
           type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == OpenGenericInterface);
}
