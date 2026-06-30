using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0022

namespace FastEndpoints.Messaging.Remote.Testing;

/// <summary>
/// WAF extension methods of integration testing gRPC event/command queues
/// </summary>
public static class Extensions
{
    /// <param name="s"></param>
    extension(IServiceCollection s)
    {
        /// <summary>
        /// enables communicating with a remote gRPC server in the WAF testing environment
        /// </summary>
        /// <param name="remote">the <see cref="TestServer" /> of the target WAF</param>
        public IServiceCollection RegisterTestRemote(TestServer remote)
            => s.AddSingleton(remote.CreateHandler());

        /// <summary>
        /// register test/fake/mock event handlers for integration testing gRPC event queues
        /// </summary>
        /// <typeparam name="TEvent">the type of the event model to register a test handler for</typeparam>
        /// <typeparam name="THandler">the type of the test event handler</typeparam>
        public IServiceCollection RegisterTestEventHandler<TEvent, THandler>()
            where TEvent : IEvent
            where THandler : class, IEventHandler<TEvent>
            => s.AddSingleton<IEventHandler<TEvent>, THandler>(); //event handlers are always singletons

        /// <summary>
        /// register test/fake/mock command handlers for integration testing gRPC commands
        /// </summary>
        /// <typeparam name="TCommand">the type of the command model to register a test handler for</typeparam>
        /// <typeparam name="THandler">the type of the test command handler</typeparam>
        public IServiceCollection RegisterTestCommandHandler<TCommand, THandler>()
            where TCommand : ICommand
            where THandler : class, ICommandHandler<TCommand>
            => s.AddSingleton<ICommandHandler<TCommand>, THandler>(); //command handlers are transient at consumption but, singleton here because we only need to resolve the type when consuming.

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

    /// <param name="provider"></param>
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