using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

internal static class EventSubscriberStorage
{
    internal static bool IsInMemoryProvider { get; private set; }
    internal static bool IsInitalized { get; private set; }
    internal static Func<IEventStorageRecord> RecordFactory { get; private set; } = default!;
    internal static IEventSubscriberStorageProvider Provider { get; private set; } = default!;

    static EventSubscriberStorage()
    {
        _ = StaleSubscriberPurgingTask();
    }

    internal static void Initialize<TStorageRecord, TStorageProvider>(IServiceProvider serviceProvider)
        where TStorageRecord : IEventStorageRecord, new()
        where TStorageProvider : class, IEventSubscriberStorageProvider
    {
        RecordFactory = () => new TStorageRecord();
        Provider = ActivatorUtilities.CreateInstance<TStorageProvider>(serviceProvider);
        IsInitalized = true;
        IsInMemoryProvider = Provider is InMemoryEventSubscriberStorage;
    }

    private static async Task StaleSubscriberPurgingTask()
    {
        bool? isDefaultProvider = null;

        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));

            isDefaultProvider ??= Provider is InMemoryEventSubscriberStorage;
            if (isDefaultProvider is true)
                break; //purging is not used in default subscriber storage

            try
            {
                await Provider.PurgeStaleRecordsAsync();
            }
            catch { }
        }
    }
}
