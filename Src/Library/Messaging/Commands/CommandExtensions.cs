using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

//key: tCommand
//val: command handler definition
class CommandHandlerRegistry : ConcurrentDictionary<Type, CommandHandlerDefinition>;

public static class CommandExtensions
{
    internal static bool TestHandlersPresent;

    /// <summary>
    /// executes the command that does not return a result
    /// </summary>
    /// <param name="command">the command to execute</param>
    /// <param name="ct">optional cancellation token</param>
    /// <exception cref="InvalidOperationException">thrown when a handler for the command cannot be instantiated</exception>
    public static Task ExecuteAsync<TCommand>(this TCommand command, CancellationToken ct = default) where TCommand : ICommand
    {
        var tCommand = command.GetType();
        var registry = Cfg.ServiceResolver.Resolve<CommandHandlerRegistry>();

        registry.TryGetValue(tCommand, out var def);

        InitGenericHandler<VoidResult>(ref def, tCommand, registry);

        if (def is null)
            throw new InvalidOperationException($"Unable to create an instance of the handler for command [{tCommand.FullName}]");

        def.HandlerExecutor ??= CreateHandlerExecutor(tCommand);

        if (TestHandlersPresent)
            def.HandlerType = Cfg.ServiceResolver.TryResolve<ICommandHandler<TCommand>>()?.GetType() ?? def.HandlerType;

        return ((ICommandHandlerExecutor)def.HandlerExecutor).Execute(command, def.HandlerType, ct);

        static ICommandHandlerExecutor CreateHandlerExecutor(Type tCommand)
            => (ICommandHandlerExecutor)Cfg.ServiceResolver.CreateSingleton(Types.CommandHandlerExecutorOf1.MakeGenericType(tCommand));
    }

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
        var registry = Cfg.ServiceResolver.Resolve<CommandHandlerRegistry>();

        registry.TryGetValue(tCommand, out var def);

        InitGenericHandler<TResult>(ref def, tCommand, registry);

        if (def is null)
            throw new InvalidOperationException($"Unable to create an instance of the handler for command [{tCommand.FullName}]");

        def.HandlerExecutor ??= CreateHandlerExecutor(tCommand);

        // ReSharper disable once InvertIf
        if (TestHandlersPresent)
        {
            var tHandlerInterface = Types.ICommandHandlerOf2.MakeGenericType(tCommand, typeof(TResult));
            def.HandlerType = Cfg.ServiceResolver.TryResolve(tHandlerInterface)?.GetType() ?? def.HandlerType;
        }

        return ((ICommandHandlerExecutor<TResult>)def.HandlerExecutor).Execute(command, def.HandlerType, ct);

        static ICommandHandlerExecutor<TResult> CreateHandlerExecutor(Type tCommand)
            => (ICommandHandlerExecutor<TResult>)
                Cfg.ServiceResolver.CreateSingleton(Types.CommandHandlerExecutorOf2.MakeGenericType(tCommand, typeof(TResult)));
    }

    /// <summary>
    /// registers a fake command handler for unit testing purposes
    /// </summary>
    /// <typeparam name="TCommand">type of the command</typeparam>
    /// <param name="handler">a fake handler instance</param>
    public static void RegisterForTesting<TCommand>(this ICommandHandler<TCommand> handler) where TCommand : ICommand
    {
        var tCommand = typeof(TCommand);
        var registry = Cfg.ServiceResolver.Resolve<CommandHandlerRegistry>();

        registry[tCommand] = new(handler.GetType())
        {
            HandlerExecutor = new FakeCommandHandlerExecutor<TCommand>(handler)
        };
    }

    /// <summary>
    /// registers a fake command handler for unit testing purposes
    /// </summary>
    /// <typeparam name="TCommand">type of the command</typeparam>
    /// <typeparam name="TResult">type of the result being returned by the handler</typeparam>
    /// <param name="handler">a fake handler instance</param>
    public static void RegisterForTesting<TCommand, TResult>(this ICommandHandler<TCommand, TResult> handler) where TCommand : ICommand<TResult>
    {
        var tCommand = typeof(TCommand);
        var registry = Cfg.ServiceResolver.Resolve<CommandHandlerRegistry>();

        registry[tCommand] = new(handler.GetType())
        {
            HandlerExecutor = new FakeCommandHandlerExecutor<TCommand, TResult>(handler)
        };
    }

    /// <summary>
    /// register a generic command handler for a generic command
    /// </summary>
    /// <typeparam name="TCommand">the type of the command</typeparam>
    /// <typeparam name="THandler">the type of the command handler</typeparam>
    /// <returns></returns>
    public static IServiceProvider RegisterGenericCommand<TCommand, THandler>(this IServiceProvider sp)
        where TCommand : ICommand
        where THandler : ICommandHandler
    {
        return RegisterGenericCommand(sp, typeof(TCommand), typeof(THandler));
    }

    /// <summary>
    /// register a generic command handler for a generic command
    /// </summary>
    /// <param name="genericCommandType">
    /// the open generic type of the command. ex: <c> typeof(MyCommand&lt;&gt;) </c>
    /// </param>
    /// <param name="genericHandlerType">the open generic type of the command handler. ex: <c> typeof(MyCommandHandler&lt;,&gt;) </c></param>
    /// <returns></returns>
    public static IServiceProvider RegisterGenericCommand(this IServiceProvider sp, Type genericCommandType, Type genericHandlerType)
    {
        var registry = sp.GetRequiredService<CommandHandlerRegistry>();

        registry[genericCommandType] = new(genericHandlerType);

        return sp;
    }

    static void InitGenericHandler<TResult>(ref CommandHandlerDefinition? def, Type tCommand, CommandHandlerRegistry registry)
    {
        if (def is not null || !tCommand.IsGenericType)
            return;

        var tGenCmd = tCommand.GetGenericTypeDefinition();

        if (!registry.TryGetValue(tGenCmd, out var genDef))
            throw new InvalidOperationException($"No generic handler registered for generic command type: [{tGenCmd.FullName}]");

        var tHnd = genDef.HandlerType.MakeGenericType(tCommand.GetGenericArguments());
        var tRes = typeof(TResult);
        var tTargetIfc = tRes == Types.VoidResult
                             ? Types.ICommandHandlerOf1.MakeGenericType(tCommand)
                             : Types.ICommandHandlerOf2.MakeGenericType(tCommand, tRes);

        if (!tHnd.IsAssignableTo(tTargetIfc))
            throw new InvalidOperationException($"The registered generic handler for the generic command [{tGenCmd.FullName}] is not the correct type!");

        def = registry[tCommand] = new(tHnd);
    }
}