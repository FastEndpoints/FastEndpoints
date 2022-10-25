using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.Extensions;

public static class CommandExtensions
{
    internal static readonly Dictionary<Type, HandlerDefinition> handlerCache = new();

    internal class HandlerDefinition
    {
        internal Type HandlerType { get; set; }
        internal ObjectFactory? HandlerCreator { get; set; }
        internal object? ExecuteMethod { get; set; }

        internal HandlerDefinition(Type handlerType, ObjectFactory? handlerCreator)
        {
            HandlerType = handlerType;
            HandlerCreator = handlerCreator;
        }
    }

    public static async Task<TResult> ExecuteAsync<TResult>(this ICommand<TResult> command, CancellationToken ct = default)
    {
        var tCommand = command.GetType();

        if (handlerCache.TryGetValue(tCommand, out var hndDef))
        {
            hndDef.HandlerCreator ??= ActivatorUtilities.CreateFactory(hndDef.HandlerType, Type.EmptyTypes);
            var handler = hndDef.HandlerCreator(IServiceResolver.RootServiceProvider, null);
            hndDef.ExecuteMethod ??= hndDef.HandlerType.HandlerExecutor<TResult>(tCommand, handler);
            return await ((Func<object, CancellationToken, Task<TResult>>)hndDef.ExecuteMethod)(command, ct);
        }
        throw new InvalidOperationException($"Unable to create an instance of the handler for command [{tCommand.FullName}]");
    }

    public static Task ExecuteAsync(this ICommand commandModel, CancellationToken cancellation = default)
    {
        return Task.CompletedTask;

        //var requestType = typeof(Command<>);
        //Type[] typeArgs = { commandModel.GetType() };

        //var requestGenericType = requestType.MakeGenericType(typeArgs);
        //var request = Activator.CreateInstance(requestGenericType);
        //if (request == null)
        //    throw new Exception($"Couldn't create an instance of the command '{requestGenericType.Name}'!.");

        //var sendMethod = request.GetType().GetMethod("ExecuteAsync")!;
        //return (Task)sendMethod.Invoke(request, new object[] { commandModel, cancellation })!;
    }
}