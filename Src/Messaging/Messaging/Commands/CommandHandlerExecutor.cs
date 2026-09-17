using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FastEndpoints;

//NOTE: CommandHandlerExecutor<> class is singleton
//      (cached in CommandHandlerDefinition.HandlerExecutor property)
interface ICommandHandlerExecutor<TResult>
{
    Task<TResult> Execute(ICommand<TResult> command, Type handlerType, CancellationToken ct);
}

sealed class CommandHandlerExecutor<TCommand, TResult>(IEnumerable<ICommandMiddleware<TCommand, TResult>> m, ICommandReceiver<TCommand>? commandReceiver = null)
    : ICommandHandlerExecutor<TResult> where TCommand : ICommand<TResult>
{
    internal ICommandHandler<TCommand, TResult>? TestHandler { get; init; }

    readonly Type[] _tMiddlewares = m.Select(x => x.GetType()).ToArray();

    public Task<TResult> Execute(ICommand<TResult> command, Type tCommandHandler, CancellationToken ct)
    {
        commandReceiver?.AddCommand((TCommand)command);

        var cmdHandler = TestHandler ?? //TestHandler is not null for unit tests
                         (ICommandHandler<TCommand, TResult>)ServiceResolver.Instance.CreateInstance(tCommandHandler);

        return InvokeMiddleware(0);

        Task<TResult> InvokeMiddleware(int index)
        {
            return index == _tMiddlewares.Length
                       ? cmdHandler.ExecuteAsync((TCommand)command, ct)
                       : ((ICommandMiddleware<TCommand, TResult>)ServiceResolver.Instance.CreateInstance(_tMiddlewares[index])).ExecuteAsync(
                           (TCommand)command,
                           () => InvokeMiddleware(index + 1),
                           ct);
        }
    }
}

// Native AOT cannot emit CommandHandlerExecutor<TCommand, Void> (Void is a struct). This executor only genericizes over the command class.
// Parameterless ctor so ActivatorUtilities does not need IEnumerable<ICommandMiddleware<TCommand, Void>> (also valuetype-closed).
// Middleware types come from CommandMiddlewareRegistrations, not GetServices of that valuetype-closed enumerable.
sealed class VoidCommandHandlerExecutor<TCommand> : ICommandHandlerExecutor<Void> where TCommand : ICommand
{
    internal ICommandHandler<TCommand, Void>? TestHandler { get; init; }

    readonly Type[] _tMiddlewares;
    readonly ICommandReceiver<TCommand>? _commandReceiver;

    public VoidCommandHandlerExecutor()
    {
        _commandReceiver = ServiceResolver.Instance.TryResolve<ICommandReceiver<TCommand>>();
        _tMiddlewares = ResolveMiddlewareTypes();
    }

    [UnconditionalSuppressMessage("AOT", "IL3050")]
    static Type[] ResolveMiddlewareTypes()
    {
        var registrations = ServiceResolver.Instance.TryResolve<CommandMiddlewareRegistrations>();
        if (registrations is null || registrations.Items.Count == 0)
            return [];

        var tClosed = typeof(ICommandMiddleware<TCommand, Void>);
        var tOpen = typeof(ICommandMiddleware<,>);
        List<Type>? types = null;

        foreach (var (tInterface, tImplementation) in registrations.Items)
        {
            if (tInterface == tClosed)
            {
                (types ??= []).Add(tImplementation);
                continue;
            }

            if (tInterface != tOpen)
                continue;

            if (!RuntimeFeature.IsDynamicCodeSupported)
            {
                throw new NotSupportedException(
                    $"Open-generic command middleware [{tImplementation.Name}] cannot wrap no-result commands under Native AOT. " +
                    $"Register a closed type with Register<{typeof(TCommand).Name}, Void, TMiddleware>().");
            }

            (types ??= []).Add(tImplementation.MakeGenericType(typeof(TCommand), typeof(Void)));
        }

        return types is null ? [] : [.. types];
    }

    public Task<Void> Execute(ICommand<Void> command, Type tCommandHandler, CancellationToken ct)
    {
        _commandReceiver?.AddCommand((TCommand)command);

        var cmdHandler = TestHandler ??
                         (ICommandHandler<TCommand>)ServiceResolver.Instance.CreateInstance(tCommandHandler);

        return InvokeMiddleware(0);

        async Task<Void> InvokeMiddleware(int index)
        {
            if (index == _tMiddlewares.Length)
            {
                await cmdHandler.ExecuteAsync((TCommand)command, ct);

                return default;
            }

            return await ((ICommandMiddleware<TCommand, Void>)ServiceResolver.Instance.CreateInstance(_tMiddlewares[index])).ExecuteAsync(
                (TCommand)command,
                () => InvokeMiddleware(index + 1),
                ct);
        }
    }
}

//NOTE: StreamCommandHandlerExecutor<> class is singleton
//      (cached in CommandHandlerDefinition.HandlerExecutor property)
interface IStreamCommandHandlerExecutor<TResult>
{
    IAsyncEnumerable<TResult> Execute(IStreamCommand<TResult> command, Type handlerType, CancellationToken ct);
}

sealed class StreamCommandHandlerExecutor<TCommand, TResult>(IEnumerable<IStreamCommandMiddleware<TCommand, TResult>> m, ICommandReceiver<TCommand>? commandReceiver = null)
    : IStreamCommandHandlerExecutor<TResult> where TCommand : IStreamCommand<TResult>
{
    internal IStreamCommandHandler<TCommand, TResult>? TestHandler { get; init; }

    readonly Type[] _tMiddlewares = m.Select(x => x.GetType()).ToArray();

    public IAsyncEnumerable<TResult> Execute(IStreamCommand<TResult> command, Type tCommandHandler, CancellationToken ct)
    {
        commandReceiver?.AddCommand((TCommand)command);

        var cmdHandler = TestHandler ?? //TestHandler is not null for unit tests
                         (IStreamCommandHandler<TCommand, TResult>)ServiceResolver.Instance.CreateInstance(tCommandHandler);

        return InvokeMiddleware(0);

        IAsyncEnumerable<TResult> InvokeMiddleware(int index)
        {
            return index == _tMiddlewares.Length
                       ? cmdHandler.ExecuteAsync((TCommand)command, ct)
                       : ((IStreamCommandMiddleware<TCommand, TResult>)ServiceResolver.Instance.CreateInstance(_tMiddlewares[index])).ExecuteAsync(
                           (TCommand)command,
                           () => InvokeMiddleware(index + 1),
                           ct);
        }
    }
}