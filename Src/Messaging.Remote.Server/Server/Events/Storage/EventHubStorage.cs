namespace FastEndpoints;

static class EventHubStorage<TStorageRecord, TStorageProvider>
    where TStorageRecord : IEventStorageRecord, new()
    where TStorageProvider : IEventHubStorageProvider<TStorageRecord>
{
    internal static TStorageProvider Provider { get; set; } = default!;
    internal static bool IsInMemProvider { private get; set; }

    static EventHubStorage()
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
                //do nothing
            }
        }
    }
}