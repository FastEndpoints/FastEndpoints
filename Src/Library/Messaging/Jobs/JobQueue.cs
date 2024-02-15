using FastEndpoints.Messaging.Jobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable MethodSupportsCancellation

namespace FastEndpoints;

abstract class JobQueueBase
{
    //key: tCommand
    //val: job queue for the command type
    //values get created when the DI container resolves each job queue type and the ctor is run.
    //see ctor in JobQueue<TCommand, TStorageRecord, TStorageProvider>
    protected static readonly ConcurrentDictionary<Type, JobQueueBase> AllQueues = new();

    protected abstract Task StoreJobAsync(ICommand command, DateTime? executeAfter, DateTime? expireOn, CancellationToken ct);

    internal abstract void SetLimits(int concurrencyLimit, TimeSpan executionTimeLimit, TimeSpan semWaitLimit);

    internal static Task AddToQueueAsync(ICommand command, DateTime? executeAfter, DateTime? expireOn, CancellationToken ct)
    {
        var tCommand = command.GetType();

        return
            !AllQueues.TryGetValue(tCommand, out var queue)
                ? throw new InvalidOperationException($"A job queue has not been registered for [{tCommand.FullName}]")
                : queue.StoreJobAsync(command, executeAfter, expireOn, ct);
    }
}

// created by DI as singleton
sealed class JobQueue<TCommand, TStorageRecord, TStorageProvider> : JobQueueBase
    where TCommand : ICommand
    where TStorageRecord : IJobStorageRecord, new()
    where TStorageProvider : IJobStorageProvider<TStorageRecord>
{
    static readonly Type _tCommand = typeof(TCommand);
    static readonly string _tCommandName = _tCommand.FullName!;

    //public due to: https://github.com/FastEndpoints/FastEndpoints/issues/468
    public static readonly string QueueID = _tCommandName.ToHash();

    readonly ParallelOptions _parallelOptions = new() { MaxDegreeOfParallelism = Environment.ProcessorCount };
    readonly CancellationToken _appCancellation;
    readonly TStorageProvider _storage;
    readonly SemaphoreSlim _sem = new(0);
    readonly ILogger _log;
    TimeSpan _executionTimeLimit = Timeout.InfiniteTimeSpan;
    TimeSpan _semWaitLimit = TimeSpan.FromSeconds(60);
    bool _isInUse;

    public JobQueue(TStorageProvider storageProvider,
                    IHostApplicationLifetime appLife,
                    ILogger<JobQueue<TCommand, TStorageRecord, TStorageProvider>> logger)
    {
        AllQueues[_tCommand] = this;
        _storage = storageProvider;
        _appCancellation = appLife.ApplicationStopping;
        _parallelOptions.CancellationToken = _appCancellation;
        _log = logger;
        JobStorage<TStorageRecord, TStorageProvider>.Provider = _storage;
        JobStorage<TStorageRecord, TStorageProvider>.AppCancellation = _appCancellation;
    }

    internal override void SetLimits(int concurrencyLimit, TimeSpan executionTimeLimit, TimeSpan semWaitLimit)
    {
        _parallelOptions.MaxDegreeOfParallelism = concurrencyLimit;
        _executionTimeLimit = executionTimeLimit;
        _semWaitLimit = semWaitLimit;
        _ = CommandExecutorTask();
    }

    protected override async Task StoreJobAsync(ICommand command, DateTime? executeAfter, DateTime? expireOn, CancellationToken ct)
    {
        _isInUse = true;
        var job = new TStorageRecord
        {
            QueueID = QueueID,
            ExecuteAfter = executeAfter ?? DateTime.UtcNow,
            ExpireOn = expireOn ?? DateTime.UtcNow.AddHours(4)
        };
        job.SetCommand((TCommand)command);
        await _storage.StoreJobAsync(job, ct);
        _sem.Release();
    }

    [SuppressMessage("Reliability", "CA2016:Forward the \'CancellationToken\' parameter to methods")]
    async Task CommandExecutorTask()
    {
        var batchSize = _parallelOptions.MaxDegreeOfParallelism * 2;

        while (!_appCancellation.IsCancellationRequested)
        {
            IEnumerable<TStorageRecord> records;

            try
            {
                records = await _storage.GetNextBatchAsync(
                              new()
                              {
                                  Limit = batchSize,
                                  QueueID = QueueID,
                                  CancellationToken = _appCancellation,
                                  Match = r => r.QueueID == QueueID &&
                                               !r.IsComplete &&
                                               DateTime.UtcNow >= r.ExecuteAfter &&
                                               DateTime.UtcNow <= r.ExpireOn
                              });
            }
            catch (Exception x)
            {
                _log.StorageRetrieveError(QueueID, _tCommandName, x.Message);
                await Task.Delay(5000);

                continue;
            }

            if (!records.Any())
            {
                // If _isInUse is false, it signifies that no job has been queued yet.
                // Therefore, there is no necessity for further iterations within the loop until the semaphore is released upon queuing the first job.
                //
                // If _isInUse is true, it indicates that we must await the queuing of the next job or the passage of delay duration (default: 1 minute), whichever comes first.
                // We must periodically reevaluate the storage status every minute to ascertain if the user has rescheduled any previous jobs while no new jobs are being queued.
                // Failure to conduct this periodic check could result in rescheduled jobs only executing upon the arrival of new jobs, potentially leading to expired job executions.
                await (_isInUse
                           ? Task.WhenAny(_sem.WaitAsync(_appCancellation), Task.Delay(_semWaitLimit))
                           : Task.WhenAny(_sem.WaitAsync(_appCancellation)));
            }
            else
                await Parallel.ForEachAsync(records, _parallelOptions, ExecuteCommand);
        }

        async ValueTask ExecuteCommand(TStorageRecord record, CancellationToken _)
        {
            try
            {
                await record.GetCommand<TCommand>()
                            .ExecuteAsync(new CancellationTokenSource(_executionTimeLimit).Token);
            }
            catch (Exception x)
            {
                _log.CommandExecutionCritical(_tCommandName, x.Message);

                while (!_appCancellation.IsCancellationRequested)
                {
                    try
                    {
                        await _storage.OnHandlerExecutionFailureAsync(record, x, _appCancellation);

                        break;
                    }
                    catch (Exception xx)
                    {
                        _log.StorageOnExecutionFailureError(QueueID, _tCommandName, xx.Message);
                        await Task.Delay(5000);
                    }
                }

                return; //abort execution here
            }

            while (!_appCancellation.IsCancellationRequested)
            {
                try
                {
                    record.IsComplete = true;
                    await _storage.MarkJobAsCompleteAsync(record, _appCancellation);

                    break;
                }
                catch (Exception x)
                {
                    _log.StorageMarkAsCompleteError(QueueID, _tCommandName, x.Message);
                    await Task.Delay(5000);
                }
            }
        }
    }
}