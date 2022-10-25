using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

public static class CommandExtensions
{
    internal static readonly Dictionary<Type, HandlerDefinition> handlerCache = new();

    internal class HandlerDefinition
    {
        internal Type HandlerType { get; set; }
        internal ObjectFactory? HandlerCreator { get; set; }
        internal object? ExecuteMethod { get; set; }

        internal HandlerDefinition(Type handlerType)
        {
            HandlerType = handlerType;
        }
    }

    public static Task<TResult> ExecuteAsync<TResult>(this ICommand<TResult> command, CancellationToken ct = default)
    {
        var tCommand = command.GetType();

        if (handlerCache.TryGetValue(tCommand, out var hndDef))
        {
            hndDef.HandlerCreator ??= ActivatorUtilities.CreateFactory(hndDef.HandlerType, Type.EmptyTypes);
            using var scope = IServiceResolver.RootServiceProvider.CreateScope();
            var handler = hndDef.HandlerCreator(scope.ServiceProvider, null);
            hndDef.ExecuteMethod ??= hndDef.HandlerType.HandlerExecutor<TResult>(tCommand, handler);
            return ((Func<object, CancellationToken, Task<TResult>>)hndDef.ExecuteMethod)(command, ct);
        }
        throw new InvalidOperationException($"Unable to create an instance of the handler for command [{tCommand.FullName}]");
    }

    public static Task ExecuteAsync(this ICommand command, CancellationToken ct = default)
    {
        var tCommand = command.GetType();

        if (handlerCache.TryGetValue(tCommand, out var hndDef))
        {
            hndDef.HandlerCreator ??= ActivatorUtilities.CreateFactory(hndDef.HandlerType, Type.EmptyTypes);
            using var scope = IServiceResolver.RootServiceProvider.CreateScope();
            var handler = hndDef.HandlerCreator(scope.ServiceProvider, null);
            hndDef.ExecuteMethod ??= hndDef.HandlerType.HandlerExecutor(tCommand, handler);
            return ((Func<object, CancellationToken, Task>)hndDef.ExecuteMethod)(command, ct);
        }
        throw new InvalidOperationException($"Unable to create an instance of the handler for command [{tCommand.FullName}]");
    }
}