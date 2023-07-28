//using Microsoft.Extensions.DependencyInjection;

//namespace FastEndpoints;

//internal static class EventHubStorage<TStorageRecord, TStorageProvider>
//    where TStorageRecord : IEventStorageRecord, new()
//    where TStorageProvider : class, IEventHubStorageProvider<TStorageRecord>
//{
//    internal static bool IsInMemoryProvider { get; private set; }
//    internal static bool IsInitialized { get; private set; }
//    internal static Func<TStorageRecord> RecordFactory { get; private set; } = default!;
//    internal static TStorageProvider Provider { get; private set; } = default!;

//    static EventHubStorage()
//    {
//        _ = StaleSubscriberPurgingTask();
//    }

//    internal static void Initialize(IServiceProvider serviceProvider)
//    {
//        RecordFactory = () => new TStorageRecord();
//        Provider = ActivatorUtilities.CreateInstance<TStorageProvider>(serviceProvider);
//        IsInitialized = true;
//        IsInMemoryProvider = Provider is InMemoryEventHubStorage;
//    }

//    private static async Task StaleSubscriberPurgingTask()
//    {
//        while (true)
//        {
//            await Task.Delay(TimeSpan.FromHours(1));
//            try
//            {
//                await Provider.PurgeStaleRecordsAsync();
//            }
//            catch { }
//        }
//    }
//}
