using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// common extension methods
/// </summary>
public static class CommonExtensions
{
    /// <summary>
    /// register a combined event storage provider for when the machine is acting as both a subscriber and event hub.
    /// </summary>
    /// <typeparam name="TStorageRecord">the type of the storage record</typeparam>
    /// <typeparam name="TStorageProvider"></typeparam>
    public static IServiceCollection AddEventStorageProvider<TStorageRecord, TStorageProvider>(this IServiceCollection services)
        where TStorageRecord : IEventStorageRecord, new()
        where TStorageProvider : class, IEventSubscriberStorageProvider<TStorageRecord>, IEventHubStorageProvider<TStorageRecord>
    {
        RemoteConnectionCore.StorageProviderType = typeof(TStorageProvider);
        RemoteConnectionCore.StorageRecordType = typeof(TStorageRecord);
        services.AddSingleton<TStorageProvider>();

        return services;
    }
}