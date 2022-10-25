using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.Extensions;

public static class CommandExtensions
{
    public static async Task<TResult> ExecuteAsync<TResult>(this ICommand<TResult> command, CancellationToken ct = default)
    {
        var tCommand = command.GetType();

        if (CommandBase.HandlerCache.TryGetValue(tCommand, out var hndDef))
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
        var requestType = typeof(Command<>);
        Type[] typeArgs = { commandModel.GetType() };

        var requestGenericType = requestType.MakeGenericType(typeArgs);
        var request = Activator.CreateInstance(requestGenericType);
        if (request == null)
            throw new Exception($"Couldn't create an instance of the command '{requestGenericType.Name}'!.");

        var sendMethod = request.GetType().GetMethod("ExecuteAsync")!;
        return (Task)sendMethod.Invoke(request, new object[] { commandModel, cancellation })!;
    }
}