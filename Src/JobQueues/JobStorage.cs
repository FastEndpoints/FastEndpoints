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

    static int _purgeTaskStarted;

    internal static void StartStaleJobPurging()
    {
        if (Interlocked.CompareExchange(ref _purgeTaskStarted, 1, 0) == 0)
            _ = StaleJobPurgingTask();
    }

    static async Task StaleJobPurgingTask()
    {
        while (!AppCancellation.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(1), AppCancellation);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await Provider.PurgeStaleJobsAsync(
                    new()
                    {
                        Match = r => r.IsComplete || r.ExpireOn <= DateTime.UtcNow,
                        CancellationToken = AppCancellation
                    });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception x)
            {
                Logger?.StoragePurgeStaleJobsError(x.Message);
            }
        }
    }
}
