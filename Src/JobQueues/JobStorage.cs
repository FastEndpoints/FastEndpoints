using Microsoft.Extensions.Logging;
using FastEndpoints.JobsQueues;

namespace FastEndpoints;

#pragma warning disable CS8618

class JobStorage<TStorageRecord, TStorageProvider>
    where TStorageRecord : IJobStorageRecord, new()
    where TStorageProvider : IJobStorageProvider<TStorageRecord>
{
    internal static TStorageProvider Provider { private get; set; }
    internal static CancellationToken AppCancellation { private get; set; }
    internal static ILogger Logger { private get; set; }

    static JobStorage()
    {
        _ = StaleJobPurgingTask();
    }

    static async Task StaleJobPurgingTask()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromHours(1));

            try
            {
                await Provider.PurgeStaleJobsAsync(
                    new()
                    {
                        Match = r => r.IsComplete || r.ExpireOn <= DateTime.UtcNow,
                        CancellationToken = AppCancellation
                    });
            }
            catch (Exception x)
            {
                Logger?.StoragePurgeStaleJobsError(x.Message);
            }
        }

        // ReSharper disable once FunctionNeverReturns
    }
}
