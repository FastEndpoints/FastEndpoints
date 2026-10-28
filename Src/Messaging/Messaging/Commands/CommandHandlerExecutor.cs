using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FastEndpoints;

//NOTE: CommandHandlerExecutor<> class is singleton
//      (cached in CommandHandlerDefinition.HandlerExecutor property)
interface ICommandHandlerExecutor<TResult>
{
    Task<TResult> Execute(ICommand<TResult> command, Type handlerType, CancellationToken ct);
}

sealed class CommandHandlerExecutor<TCommand, TResult> : ICommandHandlerExecutor<TResult> where TCommand : ICommand<TResult>
{
    internal ICommandHandler<TCommand, TResult>? TestHandler { get; init; }

    readonly Type[] _tMiddlewares;
    readonly ICommandReceiver<TCommand>? _commandReceiver;

    // DI uses this public constructor; middleware discovery follows the active request scope.
    public CommandHandlerExecutor(ICommandReceiver<TCommand>? commandReceiver = null)
        : this(ServiceResolver.Instance.Resolve<IEnumerable<ICommandMiddleware<TCommand, TResult>>>(), commandReceiver) { }

    internal CommandHandlerExecutor(IEnumerable<ICommandMiddleware<TCommand, TResult>> middleware, ICommandReceiver<TCommand>? commandReceiver = null)
    {
        _tMiddlewares = [.. middleware.Select(m => m.GetType())];
        _commandReceiver = commandReceiver;
    }

    public Task<TResult> Execute(ICommand<TResult> command, Type tCommandHandler, CancellationToken ct)
    {
        _commandReceiver?.AddCommand((TCommand)command);

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
// Native AOT middleware types come from CommandMiddlewareRegistrations to avoid resolving that valuetype-closed enumerable.
sealed class VoidCommandHandlerExecutor<TCommand> : ICommandHandlerExecutor<Void> where TCommand : ICommand
{
    internal ICommandHandler<TCommand, Void>? TestHandler { get; init; }

    readonly Type[] _tMiddlewares = ResolveMiddlewareTypes();
    readonly ICommandReceiver<TCommand>? _commandReceiver = ServiceResolver.Instance.TryResolve<ICommandReceiver<TCommand>>();

    [UnconditionalSuppressMessage("AOT", "IL3050")]
    static Type[] ResolveMiddlewareTypes()
    {
        if (RuntimeFeature.IsDynamicCodeSupported)
            return ServiceResolver.Instance.Resolve<IEnumerable<ICommandMiddleware<TCommand, Void>>>().Select(m => m.GetType()).ToArray();

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

            throw new NotSupportedException(
                $"Open-generic command middleware [{tImplementation.Name}] cannot wrap no-result commands under Native AOT. " +
                $"Register a closed type with Register<{typeof(TCommand).Name}, Void, TMiddleware>().");
        }

        return types is null ? [] : [.. types];
    }

    public Task<Void> Execute(ICommand<Void> command, Type tCommandHandler, CancellationToken ct)
    {
        _commandReceiver?.AddCommand((TCommand)command);

        var cmdHandler = TestHandler ??
                         (ICommandHandler<TCommand, Void>)ServiceResolver.Instance.CreateInstance(tCommandHandler);

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

sealed class StreamCommandHandlerExecutor<TCommand, TResult> : IStreamCommandHandlerExecutor<TResult> where TCommand : IStreamCommand<TResult>
{
    internal IStreamCommandHandler<TCommand, TResult>? TestHandler { get; init; }

    readonly Type[] _tMiddlewares;
    readonly ICommandReceiver<TCommand>? _commandReceiver;

    // DI uses this public constructor; middleware discovery follows the active request scope.
    public StreamCommandHandlerExecutor(ICommandReceiver<TCommand>? commandReceiver = null)
        : this(ServiceResolver.Instance.Resolve<IEnumerable<IStreamCommandMiddleware<TCommand, TResult>>>(), commandReceiver) { }

    internal StreamCommandHandlerExecutor(IEnumerable<IStreamCommandMiddleware<TCommand, TResult>> middleware, ICommandReceiver<TCommand>? commandReceiver = null)
    {
        _tMiddlewares = [.. middleware.Select(m => m.GetType())];
        _commandReceiver = commandReceiver;
    }

    public IAsyncEnumerable<TResult> Execute(IStreamCommand<TResult> command, Type tCommandHandler, CancellationToken ct)
    {
        _commandReceiver?.AddCommand((TCommand)command);

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