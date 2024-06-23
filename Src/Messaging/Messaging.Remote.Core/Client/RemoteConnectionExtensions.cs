using FastEndpoints.Messaging.Remote.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

/// <summary>
/// client extension methods
/// </summary>
public static class RemoteConnectionExtensions
{
    /// <summary>
    /// creates a grpc channel/connection to a remote server that hosts a known collection of command handlers and event hubs.
    /// <para>
    /// IMPORTANT: call the <see cref="RemoteConnectionCore.Register{TCommand, TResult}" /> method (using action <paramref name="r" />) to specify which commands are handled by
    /// this remote server. event subscriptions can be specified using <c>app.Subscribe&lt;TEvent, TEventHandler&gt;()</c> method.
    /// </para>
    /// </summary>
    /// <param name="host"></param>
    /// <param name="remoteAddress">the address of the remote server</param>
    /// <param name="r">a configuration action for the connection</param>
    public static IHost MapRemote(this IHost host, string remoteAddress, Action<RemoteConnectionCore> r)
    {
        r(new(remoteAddress, host.Services));

        var logger = host.Services.GetRequiredService<ILogger<RemoteConnectionCore>>();
        logger.RemoteConfigured(remoteAddress, RemoteConnectionCore.RemoteMap.Count);

        return host;
    }
}