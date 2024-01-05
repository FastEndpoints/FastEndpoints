using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FastEndpoints;

/// <summary>
/// extension methods for registering fake/test/mock command and event handlers for integration testing
/// </summary>
public static class TestingExtensions
{
    /// <summary>
    /// register test/fake/mock event handlers for integration testing events
    /// </summary>
    /// <typeparam name="TEvent">the type of the event model to register a test handler for</typeparam>
    /// <typeparam name="THandler">the type of the test event handler</typeparam>
    public static void RegisterTestEventHandler<TEvent, THandler>(this IServiceCollection s)
        where TEvent : IEvent
        where THandler : class, IEventHandler<TEvent>
        => s.AddSingleton<IEventHandler<TEvent>, THandler>(); //event handlers are always singleton

    /// <summary>
    /// register test/fake/mock command handlers for integration testing commands that don't return a result
    /// </summary>
    /// <typeparam name="TCommand">the type of the command model to register a test handler for</typeparam>
    /// <typeparam name="THandler">the type of the test command handler</typeparam>
    public static void RegisterTestCommandHandler<TCommand, THandler>(this IServiceCollection s)
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand>
    {
        s.TryAddSingleton<TestCommandHandlerMarker>();
        s.AddSingleton<ICommandHandler<TCommand>, THandler>(); //singleton here since only the type is needed at consumption.
    }

    /// <summary>
    /// register test/fake/mock command handlers for integration testing commands that returns a result
    /// </summary>
    /// <typeparam name="TCommand">the type of the command model to register a test handler for</typeparam>
    /// <typeparam name="THandler">the type of the test command handler</typeparam>
    /// <typeparam name="TResult">the type of the result</typeparam>
    public static void RegisterTestCommandHandler<TCommand, THandler, TResult>(this IServiceCollection s)
        where TCommand : ICommand<TResult>
        where THandler : class, ICommandHandler<TCommand, TResult>
    {
        s.TryAddSingleton<TestCommandHandlerMarker>();
        s.AddSingleton<ICommandHandler<TCommand, TResult>, THandler>(); //singleton here since only the type is needed at consumption.
    }
}

sealed class TestCommandHandlerMarker;