using Microsoft.Extensions.Logging;
using FastEndpoints.JobsQueues;

namespace FastEndpoints;

class JobStorage<TStorageRecord, TStorageProvider>
    where TStorageRecord : IJobStorageRecord, new()
    where TStorageProvider : IJobStorageProvider<TStorageRecord>
{
    static TStorageProvider? _provider;
    static CancellationToken _appCancellation;
    static ILogger? _logger;
    static int _initialized;

    /// <summary>
    /// Initializes the storage coordinator and starts the stale job purging background task (once per storage type).
    /// Subsequent calls are no-ops.
    /// </summary>
    internal static void Initialize(TStorageProvider provider, CancellationToken appCancellation, ILogger logger)
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
            return;

        _provider = provider;
        _appCancellation = appCancellation;
        _logger = logger;

        _ = StaleJobPurgingTask();
    }

    static async Task StaleJobPurgingTask()
    {
        while (!_appCancellation.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(1), _appCancellation);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await _provider!.PurgeStaleJobsAsync(
                    new()
                    {
                        Match = r => r.IsComplete || r.ExpireOn <= DateTime.UtcNow,
                        CancellationToken = _appCancellation
                    });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception x)
            {
                _logger?.StoragePurgeStaleJobsError(x.Message);
            }
        }
    }
}
