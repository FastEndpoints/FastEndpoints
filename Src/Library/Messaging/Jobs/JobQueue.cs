using FastEndpoints.Messaging.Jobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FastEndpoints;

abstract class JobQueueBase
{
    //key: tCommand
    //val: job queue for the command type
    //values get created when the DI container resolves each job queue type and the ctor is run.
    //see ctor in JobQueue<TCommand, TStorageRecord, TStorageProvider>
    protected static readonly ConcurrentDictionary<Type, JobQueueBase> AllQueues = new();

    protected abstract Task StoreJobAsync(ICommand command, DateTime? executeAfter, DateTime? expireOn, CancellationToken ct);

    internal abstract void SetExecutionLimits(int concurrencyLimit, TimeSpan executionTimeLimit);

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

    internal override void SetExecutionLimits(int concurrencyLimit, TimeSpan executionTimeLimit)
    {
        _parallelOptions.MaxDegreeOfParallelism = concurrencyLimit;
        _executionTimeLimit = executionTimeLimit;
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

                // ReSharper disable once MethodSupportsCancellation
                await Task.Delay(5000);

                continue;
            }

            if (!records.Any())
            {
                // if _isInUse is false, a job has never been queued and there's no need for another iteration of the while loop -
                // until the semaphore is released when the first job is queued.
                // if _isInUse if true, we need to block until the next job is queued or until 1 min has elapsed.
                // we need to re-check the storage every minute to see if the user has re-scheduled old jobs while there's no new jobs being queued.
                // without the 1 minute check, rescheduled jobs will only execute when there's a new job being queued.
                // which could lead to the rescheduled job being already expired by the time it's executed.
                await (
                          _isInUse

                              // ReSharper disable once MethodSupportsCancellation
                              ? Task.WhenAny(_sem.WaitAsync(_appCancellation), Task.Delay(60000))
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

                    #pragma warning disable CA2016

                        //ReSharper disable once MethodSupportsCancellation
                        await Task.Delay(5000);
                    #pragma warning restore CA2016
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

                #pragma warning disable CA2016

                    // ReSharper disable once MethodSupportsCancellation
                    await Task.Delay(5000);
                #pragma warning restore CA2016
                }
            }
        }
    }
}