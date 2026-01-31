using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FastEndpoints;

/// <summary>
/// extension methods for registering fake/test/mock command and event handlers for integration testing
/// </summary>
[UnconditionalSuppressMessage("aot", "IL2091")]
public static class TestingExtensions
{
    extension(IServiceCollection s)
    {
        /// <summary>
        /// register test/fake/mock command handlers for integration testing commands that don't return a result
        /// </summary>
        /// <typeparam name="TCommand">the type of the command model to register a test handler for</typeparam>
        /// <typeparam name="THandler">the type of the test command handler</typeparam>
        public void RegisterTestCommandHandler<TCommand, THandler>()
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
        public void RegisterTestCommandHandler<TCommand, THandler, TResult>()
            where TCommand : ICommand<TResult>
            where THandler : class, ICommandHandler<TCommand, TResult>
        {
            s.TryAddSingleton<TestCommandHandlerMarker>();
            s.AddSingleton<ICommandHandler<TCommand, TResult>, THandler>(); //singleton here since only the type is needed at consumption.
        }

        /// <summary>
        /// register test/fake/mock event handlers for integration testing events
        /// </summary>
        /// <typeparam name="TEvent">the type of the event model to register a test handler for</typeparam>
        /// <typeparam name="THandler">the type of the test event handler</typeparam>
        public void RegisterTestEventHandler<TEvent, THandler>()
            where TEvent : IEvent
            where THandler : class, IEventHandler<TEvent>
            => s.AddSingleton<IEventHandler<TEvent>, THandler>(); //event handlers are always singleton

        /// <summary>
        /// registers test event receivers for the purpose of testing receipt of events.
        /// </summary>
        public IServiceCollection RegisterTestEventReceivers()
            => s.AddSingleton(typeof(IEventReceiver<>), typeof(EventReceiver<>));

        /// <summary>
        /// registers test command receivers for the purpose of testing receipt of commands.
        /// </summary>
        public IServiceCollection RegisterTestCommandReceivers()
            => s.AddSingleton(typeof(ICommandReceiver<>), typeof(CommandReceiver<>));
    }

    extension(IServiceProvider provider)
    {
        /// <summary>
        /// gets a test event receiver for a given event type.
        /// </summary>
        /// <typeparam name="TEvent">the type of the event</typeparam>
        /// <exception cref="InvalidOperationException">thrown when test event receivers are not registered</exception>
        public IEventReceiver<TEvent> GetTestEventReceiver<TEvent>() where TEvent : IEvent
            => provider.GetService(typeof(IEventReceiver<TEvent>)) as IEventReceiver<TEvent> ??
               throw new InvalidOperationException("Test event receivers are not registered!");

        /// <summary>
        /// gets a test command receiver for a given command type.
        /// </summary>
        /// <typeparam name="TCommand">the type of the command</typeparam>
        /// <exception cref="InvalidOperationException">thrown when test command receivers are not registered</exception>
        public ICommandReceiver<TCommand> GetTestCommandReceiver<TCommand>() where TCommand : ICommandBase
            => provider.GetService(typeof(ICommandReceiver<TCommand>)) as ICommandReceiver<TCommand> ??
               throw new InvalidOperationException("Test command receivers are not registered!");
    }
}

sealed class TestCommandHandlerMarker;