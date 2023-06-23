using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

internal static class EventSubscriberStorage
{
    internal static bool IsInitalized;
    internal static Func<IEventStorageRecord> RecordFactory { get; set; } = default!;
    internal static IEventSubscriberStorageProvider Provider { get; set; } = default!;

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
    }

    private static async Task StaleSubscriberPurgingTask()
    {
        do
        {
            await Task.Delay(TimeSpan.FromHours(1));
            try
            {
                await Provider.PurgeStaleRecordsAsync();
            }
            catch { }
        }
        while (Provider is not InMemoryEventSubscriberStorage);
    }
}
