using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

/// <summary>
/// client extension methods
/// </summary>
public static class RemoteConnectionExtensions
{
    public static void EventSubscriberStorageProvider<TStorageRecord, TStorageProvider>(this IHost host)
        where TStorageRecord : IEventStorageRecord, new()
        where TStorageProvider : class, IEventSubscriberStorageProvider
            => EventSubscriberStorage.Initialize<TStorageRecord, TStorageProvider>(host.Services);

    /// <summary>
    /// creates a grpc channel/connection to a remote server that hosts a known collection of command handlers and event hubs.
    /// <para>
    /// IMPORTANT: call the <see cref="RemoteConnection.Register{TCommand, TResult}"/> method (using action <paramref name="r"/>) to specify which commands are handled by this remote server.
    /// event subscriptions can be specified using <see cref="RemoteConnection.Subscribe{TEvent, TEventHandler}(CallOptions)"/> method.
    /// </para>
    /// </summary>
    /// <param name="remoteAddress">the address of the remote server</param>
    /// <param name="r">a configuration action for the connection</param>
    public static IHost MapRemote(this IHost host, string remoteAddress, Action<RemoteConnection> r)
    {
        r(new RemoteConnection(remoteAddress, host.Services));
        var logger = host.Services.GetRequiredService<ILogger<RemoteConnection>>();
        logger.LogInformation(
            " Remote connection configured!\r\n Remote Server: {address}\r\n Total Commands: {count}",
            remoteAddress, RemoteConnection.RemoteMap.Count);
        return host;
    }

    /// <summary>
    /// execute the command on the relevant remote server
    /// </summary>
    /// <param name="options">call options</param>
    /// <exception cref="InvalidOperationException">thrown if the relevant remote handler has not been registered</exception>
    public static Task RemoteExecuteAsync(this ICommand command, CallOptions options = default)
    {
        var tCommand = command.GetType();

        if (!RemoteConnection.RemoteMap.TryGetValue(tCommand, out var remote))
            throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]");

        return remote.ExecuteVoid(command, tCommand, options);
    }

    /// <summary>
    /// execute the command on the relevant remote server and get back a result
    /// </summary>
    /// <typeparam name="TResult">the type of the result</typeparam>
    /// <param name="options">call options</param>
    /// <exception cref="InvalidOperationException">thrown if the relevant remote handler has not been registered</exception>
    public static Task<TResult> RemoteExecuteAsync<TResult>(this ICommand<TResult> command, CallOptions options = default) where TResult : class
    {
        var tCommand = command.GetType();

        if (!RemoteConnection.RemoteMap.TryGetValue(tCommand, out var remote))
            throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]");

        return remote.ExecuteUnary(command, tCommand, options);
    }

    /// <summary>
    /// execute the command on the relevant remote server and get back a stream of <typeparamref name="TResult"/>
    /// </summary>
    /// <typeparam name="TResult">the type of the result stream</typeparam>
    /// <param name="options">call options</param>
    /// <exception cref="InvalidOperationException">thrown if the relevant remote handler has not been registered</exception>
    public static IAsyncEnumerable<TResult> RemoteExecuteAsync<TResult>(this IServerStreamCommand<TResult> command, CallOptions options = default) where TResult : class
    {
        var tCommand = command.GetType();

        if (!RemoteConnection.RemoteMap.TryGetValue(tCommand, out var remote))
            throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]");

        return remote.ExecuteServerStream(command, tCommand, options).ReadAllAsync(options.CancellationToken);
    }

    /// <summary>
    /// send the stream of <typeparamref name="T"/> to the relevant remote server and get back a result of <typeparamref name="TResult"/>
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

        if (!RemoteConnection.RemoteMap.TryGetValue(tCommand, out var remote))
            throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]");

        return remote.ExecuteClientStream<T, TResult>(commands, tCommand, options);
    }
}