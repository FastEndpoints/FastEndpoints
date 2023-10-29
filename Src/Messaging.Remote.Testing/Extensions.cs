#pragma warning disable IDE0022
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.Messaging.Remote.Testing;

/// <summary>
/// WAF extension methods of integration testing gRPC event/command queues
/// </summary>
public static class Extensions
{
    /// <summary>
    /// enables communicating with a remote gRPC server in the WAF testing environment
    /// </summary>
    /// <param name="s"></param>
    /// <param name="remote">the <see cref="TestServer" /> of the target WAF</param>
    public static void RegisterTestRemote(this IServiceCollection s, TestServer remote)
        => s.AddSingleton(remote.CreateHandler());

    /// <summary>
    /// register test/fake/mock event handlers for integration testing gRPC event queues
    /// </summary>
    /// <typeparam name="TEvent">the type of the event model to register a test handler for</typeparam>
    /// <typeparam name="THandler">the type of the test event handler</typeparam>
    public static void RegisterTestEventHandler<TEvent, THandler>(this IServiceCollection s)
        where TEvent : IEvent
        where THandler : class, IEventHandler<TEvent>
    {
        //event handlers are always singletons
        s.AddSingleton<IEventHandler<TEvent>, THandler>();
    }

    /// <summary>
    /// register test/fake/mock command handlers for integration testing gRPC commands
    /// </summary>
    /// <typeparam name="TCommand">the type of the command model to register a test handler for</typeparam>
    /// <typeparam name="THandler">the type of the test command handler</typeparam>
    public static void RegisterTestCommandHandler<TCommand, THandler>(this IServiceCollection s)
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand>
    {
        //command handlers are transient at consumption but,
        //singleton here because we only need to resolve the type when consuming.
        s.AddSingleton<ICommandHandler<TCommand>, THandler>();
    }
}