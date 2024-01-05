namespace FastEndpoints;

static class EventSubscriberStorage<TStorageRecord, TStorageProvider>
    where TStorageRecord : IEventStorageRecord, new()
    where TStorageProvider : IEventSubscriberStorageProvider<TStorageRecord>
{
    internal static TStorageProvider Provider { private get; set; } = default!;
    internal static bool IsInMemProvider { private get; set; }

    static EventSubscriberStorage()
    {
        _ = StaleJobPurgingTask();
    }

    static async Task StaleJobPurgingTask()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromHours(1));

            if (IsInMemProvider)
                break;

            try
            {
                await Provider.PurgeStaleRecordsAsync(
                    new()
                    {
                        CancellationToken = CancellationToken.None,
                        Match = r => r.IsComplete || DateTime.UtcNow >= r.ExpireOn
                    });
            }
            catch
            {
                //do nothing }
            }
        }
    }
}