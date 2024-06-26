using FastEndpoints.Messaging.Remote.Core;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

/// <summary>
/// client extension methods
/// </summary>
public static class RemoteConnectionCoreExtensions
{
    /// <summary>
    /// creates a grpc channel/connection to a remote server that hosts a known collection of command handlers and event hubs.
    /// <para>
    /// IMPORTANT: call the <see cref="RemoteConnectionCore.Register{TCommand, TResult}" /> method (using action <paramref name="r" />) to specify which commands are handled by
    /// this remote server. event subscriptions can be specified using <c>app.Subscribe&lt;TEvent, TEventHandler&gt;()</c> method.
    /// </para>
    /// </summary>
    /// <param name="services"></param>
    /// <param name="remoteAddress">the address of the remote server</param>
    /// <param name="r">a configuration action for the connection</param>
    public static IServiceProvider MapRemoteCore(this IServiceProvider services, string remoteAddress, Action<RemoteConnectionCore> r)
    {
        r(new(remoteAddress, services));

        var logger = services.GetRequiredService<ILogger<RemoteConnectionCore>>();
        logger.RemoteConfigured(remoteAddress, RemoteConnectionCore.RemoteMap.Count);

        return services;
    }

    /// <summary>
    /// execute the command on the relevant remote server
    /// </summary>
    /// <param name="command"></param>
    /// <param name="options">call options</param>
    /// <exception cref="InvalidOperationException">thrown if the relevant remote handler has not been registered</exception>
    public static Task RemoteExecuteAsync(this ICommand command, CallOptions options = default)
    {
        var tCommand = command.GetType();

        return
            RemoteConnectionCore.RemoteMap.TryGetValue(tCommand, out var remote)
                ? remote.ExecuteVoid(command, tCommand, options)
                : throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]");
    }

    /// <summary>
    /// execute the command on the relevant remote server and get back a result
    /// </summary>
    /// <typeparam name="TResult">the type of the result</typeparam>
    /// <param name="command"></param>
    /// <param name="options">call options</param>
    /// <exception cref="InvalidOperationException">thrown if the relevant remote handler has not been registered</exception>
    public static Task<TResult> RemoteExecuteAsync<TResult>(this ICommand<TResult> command, CallOptions options = default) where TResult : class
    {
        var tCommand = command.GetType();

        return
            RemoteConnectionCore.RemoteMap.TryGetValue(tCommand, out var remote)
                ? remote.ExecuteUnary(command, tCommand, options)
                : throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]");
    }

    /// <summary>
    /// execute the command on the relevant remote server and get back a stream of <typeparamref name="TResult" />
    /// </summary>
    /// <typeparam name="TResult">the type of the result stream</typeparam>
    /// <param name="command"></param>
    /// <param name="options">call options</param>
    /// <exception cref="InvalidOperationException">thrown if the relevant remote handler has not been registered</exception>
    public static IAsyncEnumerable<TResult> RemoteExecuteAsync<TResult>(this IServerStreamCommand<TResult> command, CallOptions options = default) where TResult : class
    {
        var tCommand = command.GetType();

        return
            RemoteConnectionCore.RemoteMap.TryGetValue(tCommand, out var remote)
                ? remote.ExecuteServerStream(command, tCommand, options).ReadAllAsync(options.CancellationToken)
                : throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]");
    }

    /// <summary>
    /// send the stream of <typeparamref name="T" /> to the relevant remote server and get back a result of <typeparamref name="TResult" />
    /// </summary>
    /// <typeparam name="T">the type of item in the stream</typeparam>
    /// <typeparam name="TResult">the type of the result that will be returned when the stream ends</typeparam>
    /// <param name="commands">the stream to send</param>
    /// <param name="options">call options</param>
    /// <exception cref="InvalidOperationException">thrown if the relevant remote handler has not been registered</exception>
    public static Task<TResult> RemoteExecuteAsync<T, TResult>(this IAsyncEnumerable<T> commands, CallOptions options = default)
        where T : class
        where TResult : class
    {
        var tCommand = typeof(IAsyncEnumerable<T>);

        return
            RemoteConnectionCore.RemoteMap.TryGetValue(tCommand, out var remote)
                ? remote.ExecuteClientStream<T, TResult>(commands, tCommand, options)
                : throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]");
    }
}