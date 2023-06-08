using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

/// <summary>
/// client extension methods
/// </summary>
public static class RemoteConnectionExtensions
{
    //key: tCommand
    //val: remote server that has handlers
    internal static readonly Dictionary<Type, RemoteConnection> CommandToRemoteMap = new();

    /// <summary>
    /// creates a grpc channel/connection to a remote server that hosts a known collection of command handlers.
    /// <para>
    /// IMPORTANT: call the <see cref="RemoteConnection.Register{TCommand, TResult}"/> method (using action <paramref name="r"/>) to specify which commands are handled by this remote server.
    /// </para>
    /// </summary>
    /// <param name="remoteAddress">the address of the remote server</param>
    /// <param name="r">a configuration action for the connection</param>
    public static IHost MapRemoteHandlers(this IHost host, string remoteAddress, Action<RemoteConnection> r)
    {
        r(new RemoteConnection(remoteAddress));
        var logger = host.Services.GetRequiredService<ILogger<MessagingClient>>();
        logger.LogInformation(
            " Remote connection configured!\r\n Remote Server: {address}\r\n Total Commands: {count}",
            remoteAddress, CommandToRemoteMap.Count);
        return host;
    }

    /// <summary>
    /// execute the command on the relevant remote server and get back a result
    /// </summary>
    /// <typeparam name="TResult">the type of the result</typeparam>
    /// <param name="ct">cancellation token</param>
    /// <exception cref="InvalidOperationException">thrown if the relevant remote handler has not been registered</exception>
    public static Task<TResult> RemoteExecuteAsync<TResult>(this ICommand<TResult> command, CancellationToken ct = default) where TResult : class
    {
        var tCommand = command.GetType();

        if (!CommandToRemoteMap.TryGetValue(tCommand, out var remote))
            throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]");

        return remote.Execute(command, tCommand, ct);
    }

    //only used by integration tests
    public static Task<TResult> TestRemoteExecuteAsync<TCommand, TResult>(this ICommand<TResult> command, HttpMessageHandler httpMessageHandler)
        where TCommand : class, ICommand<TResult>
        where TResult : class
    {
        var remote = new RemoteConnection("http://testhost");
        remote.ChannelOptions.HttpHandler = httpMessageHandler;
        remote.Register<TCommand, TResult>();
        return remote.Execute(command, typeof(TCommand), default);
    }
}

internal sealed class MessagingClient { }