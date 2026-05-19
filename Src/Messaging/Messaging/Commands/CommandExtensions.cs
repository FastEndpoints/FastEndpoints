using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using FastEndpoints.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

//key: tCommand
//val: command handler definition

/// <summary>
/// registry for command handlers. maps command types to their handler definitions.
/// </summary>
internal sealed class CommandHandlerRegistry : ConcurrentDictionary<Type, CommandHandlerDefinition>;

/// <summary>
/// extension methods for command execution
/// </summary>
[UnconditionalSuppressMessage("aot", "IL3050"), UnconditionalSuppressMessage("aot", "IL2055"), UnconditionalSuppressMessage("aot", "IL2077")]
public static class CommandExtensions
{
    internal static Type? TestCommandHandlerMarker;

    /// <summary>
    /// executes the command that does not return a result
    /// </summary>
    /// <param name="command">the command to execute</param>
    /// <param name="ct">optional cancellation token</param>
    /// <exception cref="InvalidOperationException">thrown when a handler for the command cannot be instantiated</exception>
    public static Task ExecuteAsync<TCommand>(this TCommand command, CancellationToken ct = default) where TCommand : class, ICommand
        => command.ExecuteAsync<Void>(ct);

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
        var registry = ServiceResolver.Instance.Resolve<CommandHandlerRegistry>();
        registry.TryGetValue(tCommand, out var def);
        var tRes = typeof(TResult);
        var tHandlerInterface = tRes == Types.VoidResult
                                    ? Types.ICommandHandlerOf1.MakeGenericType(tCommand)
                                    : Types.ICommandHandlerOf2.MakeGenericType(tCommand, tRes);

        if (def is null && tCommand.IsGenericType)
            InitGenericHandlerCore(ref def, tCommand, registry, tHandlerInterface);

        var tHandler = PrepareExecution<TResult>(def, tCommand, Types.CommandHandlerExecutorOf2, tHandlerInterface);

        return ((ICommandHandlerExecutor<TResult>)def!.HandlerExecutor!).Execute(command, tHandler, ct);
    }

    /// <summary>
    /// executes the command and returns a stream of results
    /// </summary>
    /// <typeparam name="TResult">the type of items in the returned stream</typeparam>
    /// <param name="command">the command to execute</param>
    /// <param name="ct">optional cancellation token</param>
    /// <exception cref="InvalidOperationException">thrown when a handler for the command cannot be instantiated</exception>
    public static IAsyncEnumerable<TResult> ExecuteAsync<TResult>(this IStreamCommand<TResult> command, CancellationToken ct = default)
    {
        var tCommand = command.GetType();
        var registry = ServiceResolver.Instance.Resolve<CommandHandlerRegistry>();
        registry.TryGetValue(tCommand, out var def);

        if (def is null && tCommand.IsGenericType)
            InitGenericHandlerCore(ref def, tCommand, registry, Types.IStreamCommandHandlerOf2.MakeGenericType(tCommand, typeof(TResult)));

        var tHandler = PrepareExecution<TResult>(def, tCommand, Types.StreamCommandHandlerExecutorOf2, Types.IStreamCommandHandlerOf2.MakeGenericType(tCommand, typeof(TResult)));

        return ((IStreamCommandHandlerExecutor<TResult>)def!.HandlerExecutor!).Execute(command, tHandler, ct);
    }

    static void InitGenericHandlerCore(ref CommandHandlerDefinition? def, Type tCommand, CommandHandlerRegistry registry, Type tTargetIfc)
    {
        var tGenCmd = tCommand.GetGenericTypeDefinition();

        if (!registry.TryGetValue(tGenCmd, out var genDef))
            throw new InvalidOperationException($"No generic handler registered for generic {Kind(tTargetIfc)} type: [{tGenCmd.FullName}]");

        var tHnd = genDef.HandlerType.MakeGenericType(tCommand.GetGenericArguments());

        if (!tHnd.IsAssignableTo(tTargetIfc))
            throw new InvalidOperationException($"The registered generic handler for the generic {Kind(tTargetIfc)} [{tGenCmd.FullName}] is not the correct type!");

        def = registry[tCommand] = new(tHnd);

        static string Kind(Type t)
            => t.IsGenericType && t.GetGenericTypeDefinition() == Types.IStreamCommandHandlerOf2 ? "stream command" : "command";
    }

    static Type PrepareExecution<TResult>(CommandHandlerDefinition? def, Type tCommand, Type tExecutorOpenGeneric, Type tHandlerInterface)
    {
        if (def is null)
            throw new InvalidOperationException($"Unable to create an instance of the handler for command [{tCommand.FullName}]");

        var resolver = ServiceResolver.Instance;

        def.HandlerExecutor ??= resolver.CreateSingleton(tExecutorOpenGeneric.MakeGenericType(tCommand, typeof(TResult)));

        return TestCommandHandlerMarker is not null && resolver.TryResolve(TestCommandHandlerMarker) is not null
                   ? resolver.TryResolve(tHandlerInterface)?.GetType() ?? def.HandlerType
                   : def.HandlerType;
    }

    /// <summary>
    /// registers a fake command handler for unit testing purposes
    /// </summary>
    /// <typeparam name="TCommand">type of the command</typeparam>
    /// <param name="handler">a fake handler instance</param>
    public static void RegisterForTesting<TCommand>(this ICommandHandler<TCommand, Void> handler) where TCommand : ICommand
        => RegisterHandlerForTesting(
            typeof(TCommand),
            handler.GetType(),
            new CommandHandlerExecutor<TCommand, Void>(ServiceResolver.Instance.Resolve<IEnumerable<ICommandMiddleware<TCommand, Void>>>())
            {
                TestHandler = handler
            });

    /// <summary>
    /// registers a fake command handler for unit testing purposes
    /// </summary>
    /// <typeparam name="TCommand">type of the command</typeparam>
    /// <typeparam name="TResult">type of the result being returned by the handler</typeparam>
    /// <param name="handler">a fake handler instance</param>
    public static void RegisterForTesting<TCommand, TResult>(this ICommandHandler<TCommand, TResult> handler) where TCommand : ICommand<TResult>
        => RegisterHandlerForTesting(
            typeof(TCommand),
            handler.GetType(),
            new CommandHandlerExecutor<TCommand, TResult>(ServiceResolver.Instance.Resolve<IEnumerable<ICommandMiddleware<TCommand, TResult>>>())
            {
                TestHandler = handler
            });

    /// <summary>
    /// registers a fake stream command handler for unit testing purposes
    /// </summary>
    /// <typeparam name="TCommand">type of the command</typeparam>
    /// <typeparam name="TResult">type of the items in the result stream</typeparam>
    /// <param name="handler">a fake handler instance</param>
    public static void RegisterForTesting<TCommand, TResult>(this IStreamCommandHandler<TCommand, TResult> handler) where TCommand : IStreamCommand<TResult>
        => RegisterHandlerForTesting(
            typeof(TCommand),
            handler.GetType(),
            new StreamCommandHandlerExecutor<TCommand, TResult>(ServiceResolver.Instance.Resolve<IEnumerable<IStreamCommandMiddleware<TCommand, TResult>>>())
            {
                TestHandler = handler
            });

    static void RegisterHandlerForTesting(Type tCommand, Type tHandlerType, object executor)
        => ServiceResolver.Instance.Resolve<CommandHandlerRegistry>()[tCommand] = new(tHandlerType)
        {
            HandlerExecutor = executor
        };

    /// <param name="sp">the service provider</param>
    extension(IServiceProvider sp)
    {
        /// <summary>
        /// register a generic command handler for a generic command that returns no result.
        /// </summary>
        /// <typeparam name="TCommand">the type of the command</typeparam>
        /// <typeparam name="THandler">the type of the command handler</typeparam>
        /// <returns>the service provider for chaining</returns>
        public IServiceProvider RegisterGenericCommand<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TCommand,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            THandler>() where TCommand : ICommand where THandler : ICommandHandler
            => sp.RegisterGenericCommand(typeof(TCommand), typeof(THandler));

        /// <summary>
        /// register a generic command handler for a generic command that returns a result.
        /// </summary>
        /// <typeparam name="TCommand">the generic command type</typeparam>
        /// <typeparam name="TResult">the result type</typeparam>
        /// <typeparam name="THandler">the generic command handler type</typeparam>
        public IServiceProvider RegisterGenericCommand<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TCommand,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TResult,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            THandler>() where TCommand : ICommand<TResult> where THandler : ICommandHandler<TCommand, TResult>
            => sp.RegisterGenericCommand(typeof(TCommand), typeof(THandler));

        /// <summary>
        /// register a stream command handler for a closed generic stream command.
        /// </summary>
        /// <typeparam name="TCommand">the stream command type</typeparam>
        /// <typeparam name="TResult">the type of items in the result stream</typeparam>
        /// <typeparam name="THandler">the stream command handler type</typeparam>
        public IServiceProvider RegisterStreamCommand<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TCommand,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TResult,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            THandler>() where TCommand : IStreamCommand<TResult> where THandler : IStreamCommandHandler<TCommand, TResult>
        {
            var registry = sp.GetRequiredService<CommandHandlerRegistry>();

            registry[typeof(TCommand)] = new(typeof(THandler))
            {
                HandlerExecutor = new StreamCommandHandlerExecutor<TCommand, TResult>(
                    sp.GetServices<IStreamCommandMiddleware<TCommand, TResult>>(),
                    sp.GetService<ICommandReceiver<TCommand>>())
            };

            return sp;
        }

        /// <summary>
        /// register a generic stream command handler for a generic command that returns a stream of results.
        /// </summary>
        /// <typeparam name="TCommand">the generic stream command type</typeparam>
        /// <typeparam name="TResult">the type of items in the result stream</typeparam>
        /// <typeparam name="THandler">the generic stream command handler type</typeparam>
        public IServiceProvider RegisterGenericStreamCommand<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TCommand,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TResult,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            THandler>() where TCommand : IStreamCommand<TResult> where THandler : IStreamCommandHandler<TCommand, TResult>
            => sp.RegisterGenericStreamCommand(typeof(TCommand), typeof(THandler));

        /// <summary>
        /// register a generic stream command handler for a generic stream command.
        /// </summary>
        /// <param name="genericCommandType">
        /// the open generic type of the stream command. ex: <c> typeof(MyStreamCommand&lt;&gt;) </c>
        /// </param>
        /// <param name="genericHandlerType">the open generic type of the stream command handler. ex: <c> typeof(MyStreamCommandHandler&lt;&gt;) </c></param>
        /// <returns>the service provider for chaining</returns>
        public IServiceProvider RegisterGenericStreamCommand(Type genericCommandType, Type genericHandlerType)
            => sp.RegisterGenericCommand(genericCommandType, genericHandlerType);

        /// <summary>
        /// register a generic command handler for a generic command
        /// </summary>
        /// <param name="genericCommandType">
        /// the open generic type of the command. ex: <c> typeof(MyCommand&lt;&gt;) </c>
        /// </param>
        /// <param name="genericHandlerType">the open generic type of the command handler. ex: <c> typeof(MyCommandHandler&lt;,&gt;) </c></param>
        /// <returns>the service provider for chaining</returns>
        public IServiceProvider RegisterGenericCommand(Type genericCommandType, Type genericHandlerType)
        {
            var registry = sp.GetRequiredService<CommandHandlerRegistry>();

            registry[genericCommandType] = new(genericHandlerType);

            return sp;
        }
    }

    /// <param name="services">the service collection</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// register a common middleware pipeline for command handlers. the middleware can be created as open generic classes that implement the
        /// <see cref="ICommandMiddleware{TCommand,TResult}" /> interface as well as closed generic classes implementing the same interface.
        /// </summary>
        /// <param name="config">configuration action for adding middleware components to the pipeline</param>
        /// <returns>the service collection for chaining</returns>
        public IServiceCollection AddCommandMiddleware(Action<CommandMiddlewareConfig> config)
        {
            var c = new CommandMiddlewareConfig();
            config(c);

            return AddMiddlewarePipeline(services, c, nameof(config));
        }

        /// <summary>
        /// register a common middleware pipeline for stream command handlers. the middleware can be created as open generic classes that implement the
        /// <see cref="IStreamCommandMiddleware{TCommand,TResult}" /> interface as well as closed generic classes implementing the same interface.
        /// </summary>
        /// <param name="config">configuration action for adding middleware components to the pipeline</param>
        /// <returns>the service collection for chaining</returns>
        public IServiceCollection AddStreamCommandMiddleware(Action<StreamCommandMiddlewareConfig> config)
        {
            var c = new StreamCommandMiddlewareConfig();
            config(c);

            return AddMiddlewarePipeline(services, c, nameof(config));
        }
    }

    static IServiceCollection AddMiddlewarePipeline(IServiceCollection services, CommandMiddlewareConfigBase config, string paramName)
    {
        if (config.Middleware.Count == 0)
            throw new ArgumentNullException(paramName, "Please add some command middleware to the pipeline!");

        foreach (var mw in config.Middleware)
            services.AddTransient(mw.tInterface, mw.tImplementation);

        return services;
    }
}