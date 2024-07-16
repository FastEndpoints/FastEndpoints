using FastEndpoints.Messaging.Remote.Core;
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
    /// <summary>
    /// creates a grpc channel/connection to a remote server that hosts a known collection of command handlers and event hubs.
    /// <para>
    /// IMPORTANT: call the <see cref="RemoteConnectionCore.Register{TCommand,TResult}" /> method (using action <paramref name="r" />) to specify which commands are handled by
    /// this remote server. event subscriptions can be specified using <c>app.Subscribe&lt;TEvent, TEventHandler&gt;()</c> method.
    /// </para>
    /// </summary>
    /// <param name="host"></param>
    /// <param name="remoteAddress">the address of the remote server</param>
    /// <param name="r">a configuration action for the connection</param>
    public static IHost MapRemote(this IHost host, string remoteAddress, Action<RemoteConnection> r)
    {
        r(new(remoteAddress, host.Services));

        var logger = host.Services.GetRequiredService<ILogger<RemoteConnection>>();
        logger.RemoteConfigured(remoteAddress, RemoteConnectionCore.RemoteMap.Count);

        return host;
    }

    /// <summary>
    /// register a custom event subscriber storage provider
    /// </summary>
    /// <typeparam name="TStorageRecord">the type of the storage record</typeparam>
    /// <typeparam name="TStorageProvider"></typeparam>
    public static void AddEventSubscriberStorageProvider<TStorageRecord, TStorageProvider>(this IServiceCollection services)
        where TStorageRecord : IEventStorageRecord, new()
        where TStorageProvider : class, IEventSubscriberStorageProvider<TStorageRecord>
    {
        RemoteConnectionCore.StorageProviderType = typeof(TStorageProvider);
        RemoteConnectionCore.StorageRecordType = typeof(TStorageRecord);
        services.AddSingleton<TStorageProvider>();
    }

    /// <summary>
    /// publish the event to the relevant remote server that's running in <see cref="HubMode.EventBroker" />
    /// </summary>
    /// <param name="event"></param>
    /// <param name="options">call options</param>
    /// <exception cref="InvalidOperationException">thrown if the relevant remote handler has not been registered</exception>
    public static Task RemotePublishAsync(this IEvent @event, CallOptions options = default)
    {
        var tEvent = @event.GetType();

        return
            RemoteConnectionCore.RemoteMap.TryGetValue(tEvent, out var remote)
                ? ((RemoteConnection)remote).PublishEvent(@event, tEvent, options)
                : throw new InvalidOperationException($"No remote broker has been mapped for the event: [{tEvent.FullName}]");
    }
}