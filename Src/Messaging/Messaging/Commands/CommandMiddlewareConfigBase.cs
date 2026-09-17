using System.Diagnostics.CodeAnalysis;

namespace FastEndpoints;

public abstract class CommandMiddlewareConfigBase
{
    const string AotWarning = "open-generic middleware registration is not compatible with native aot/trimming. " +
                              "use the generic Register<TCommand, TResult, TMiddleware>() method instead.";

    internal List<(Type tInterface, Type tImplementation)> Middleware { get; } = [];

    protected abstract Type OpenGenericInterface { get; }

    [RequiresUnreferencedCode(AotWarning)]
    public void Register(params Type[] middlewareTypes)
    {
        for (var i = 0; i < middlewareTypes.Length; i++)
        {
            var tMiddleware = middlewareTypes[i];

            if (!IsValid(tMiddleware, OpenGenericInterface))
            {
                var args = OpenGenericInterface.GetGenericArguments();
                var argNames = string.Join(", ", args.Select(a => a.Name));
                var friendlyName = $"{OpenGenericInterface.Name.Split('`')[0]}<{argNames}>";

                throw new ArgumentException($"{tMiddleware.Name} must be an open generic type implementing {friendlyName}");
            }

            Middleware.Add((OpenGenericInterface, tMiddleware));
        }
    }

    static bool IsValid([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type tMiddleware, Type openGenericInterface)
        => tMiddleware.IsGenericTypeDefinition &&
           tMiddleware.GetGenericArguments().Length == 2 &&
           tMiddleware.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface);
}

// Native AOT cannot resolve IEnumerable<ICommandMiddleware<TCommand, Void>> (Void is a valuetype).
// AddCommandMiddleware records implementation types here so VoidCommandHandlerExecutor can build the pipeline without that closed generic.
sealed class CommandMiddlewareRegistrations
{
    internal List<(Type tInterface, Type tImplementation)> Items { get; } = [];
}
