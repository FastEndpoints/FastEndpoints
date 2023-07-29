namespace FastEndpoints;

internal static class JobStorage<TStorageRecord, TStorageProvider>
    where TStorageRecord : IJobStorageRecord, new()
    where TStorageProvider : IJobStorageProvider<TStorageRecord>
{
    internal static TStorageProvider Provider { private get; set; }
    internal static CancellationToken AppCancellation { private get; set; }

    static JobStorage()
    {
        _ = StaleJobPurgingTask();
    }

    private static async Task StaleJobPurgingTask()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromHours(1));

            try
            {
                await Provider.PurgeStaleJobsAsync(new()
                {
                    Match = r => r.IsComplete || r.ExpireOn <= DateTime.UtcNow,
                    CancellationToken = AppCancellation
                });
            }
            catch { }
        }
    }
}