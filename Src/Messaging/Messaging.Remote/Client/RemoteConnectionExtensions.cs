using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// client extension methods
/// </summary>
public static class RemoteConnectionExtensions
{
    /// <summary>
    /// register a custom event subscriber storage provider
    /// </summary>
    /// <typeparam name="TStorageRecord">the type of the storage record</typeparam>
    /// <typeparam name="TStorageProvider"></typeparam>
    public static void AddEventSubscriberStorageProvider<TStorageRecord, TStorageProvider>(this IServiceCollection services)
        where TStorageRecord : IEventStorageRecord, new()
        where TStorageProvider : class, IEventSubscriberStorageProvider<TStorageRecord>
    {
        RemoteConnection.StorageProviderType = typeof(TStorageProvider);
        RemoteConnection.StorageRecordType = typeof(TStorageRecord);
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