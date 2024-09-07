using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using FastEndpoints.Messaging.Jobs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

abstract class JobQueueBase
{
    //key: tCommand
    //val: job queue for the command type
    //values get created when the DI container resolves each job queue type and the ctor is run.
    //see ctor in JobQueue<TCommand, TStorageRecord, TStorageProvider>
    protected static readonly ConcurrentDictionary<Type, JobQueueBase> JobQueues = new();

    protected internal abstract Task CancelJobAsync(Guid trackingId, CancellationToken ct);

    internal abstract void SetLimits(int concurrencyLimit, TimeSpan executionTimeLimit, TimeSpan semWaitLimit);
}

abstract class JobQueueWithoutReturnValue : JobQueueBase
{
    protected abstract Task<Guid> StoreJobAsync(ICommand command, DateTime? executeAfter, DateTime? expireOn, CancellationToken ct);

    internal static Task<Guid> AddToQueueAsync(ICommand command, DateTime? executeAfter, DateTime? expireOn, CancellationToken ct)
    {
        if (executeAfter?.Kind is not null and not DateTimeKind.Utc ||
            expireOn?.Kind is not null and not DateTimeKind.Utc)
            throw new ArgumentException($"Only UTC dates are accepted for '{nameof(executeAfter)}' & '{nameof(expireOn)}' parameters!");

        var tCommand = command.GetType();

        return
            !JobQueues.TryGetValue(tCommand, out var queue)
                ? throw new InvalidOperationException($"A job queue has not been registered for [{tCommand.FullName}]")
                : queue is JobQueueWithoutReturnValue queueWithoutReturnValue
                    ? queueWithoutReturnValue.StoreJobAsync(command, executeAfter, expireOn, ct)
                    : throw new InvalidOperationException($"A job queue without return value has not been registered for [{tCommand.FullName}]");
    }

    internal static Task CancelJobAsync<TCommand>(Guid trackingId, CancellationToken ct) where TCommand : ICommand
    {
        var tCommand = typeof(TCommand);

        return
            !JobQueues.TryGetValue(tCommand, out var queue)
                ? throw new InvalidOperationException($"A job queue has not been registered for [{tCommand.FullName}]")
                : queue.CancelJobAsync(trackingId, ct);
    }
}


[SuppressMessage("Reliability", "CA2016:Forward the \'CancellationToken\' parameter to methods"), SuppressMessage("ReSharper", "MethodSupportsCancellation")]

// created by DI as singleton
sealed class JobQueue<TCommand, TStorageRecord, TStorageProvider> : JobQueueWithoutReturnValue
    where TCommand : ICommand
    where TStorageRecord : IJobStorageRecord, new()
    where TStorageProvider : IJobStorageProvider<TStorageRecord>
{
    static readonly Type _tCommand = typeof(TCommand);
    static readonly string _tCommandName = _tCommand.FullName!;

    //public due to: https://github.com/FastEndpoints/FastEndpoints/issues/468
    public static readonly string QueueID = _tCommandName.ToHash();

    readonly MemoryCache _cancellations = new(new MemoryCacheOptions());
    readonly ParallelOptions _parallelOptions = new() { MaxDegreeOfParallelism = Environment.ProcessorCount };
    readonly CancellationToken _appCancellation;
    readonly TStorageProvider _storage;
    readonly SemaphoreSlim _sem = new(0);
    readonly ILogger _log;
    TimeSpan _executionTimeLimit;
    TimeSpan _cancellationExpiry;
    TimeSpan _semWaitLimit;
    bool _isInUse;

    public JobQueue(TStorageProvider storageProvider,
                    IHostApplicationLifetime appLife,
                    ILogger<JobQueue<TCommand, TStorageRecord, TStorageProvider>> logger)
    {
        JobQueues[_tCommand] = this;
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
        _cancellationExpiry = executionTimeLimit + TimeSpan.FromHours(1);
        _semWaitLimit = semWaitLimit;
        _ = CommandExecutorTask();
    }

    protected override async Task<Guid> StoreJobAsync(ICommand command, DateTime? executeAfter, DateTime? expireOn, CancellationToken ct)
    {
        _isInUse = true;
        var job = new TStorageRecord
        {
            TrackingID = Guid.NewGuid(),
            QueueID = QueueID,
            ExecuteAfter = executeAfter ?? DateTime.UtcNow,
            ExpireOn = expireOn ?? DateTime.UtcNow.AddHours(4)
        };
        job.SetCommand((TCommand)command);
        await _storage.StoreJobAsync(job, ct);
        _sem.Release();

        return job.TrackingID;
    }

    protected internal override async Task CancelJobAsync(Guid trackingId, CancellationToken ct)
    {
        _ = AttemptCancellationTask(trackingId, _cancellations, _cancellationExpiry);
        await _storage.CancelJobAsync(trackingId, ct);

        static async Task AttemptCancellationTask(Guid trackingId, MemoryCache cancellations, TimeSpan cancelExpiry)
        {
            var cts = cancellations.GetOrCreate<CancellationTokenSource?>(
                trackingId,
                e =>
                {
                    e.SlidingExpiration = cancelExpiry;

                    return null;
                });

            var startTime = DateTime.Now;

            //this is probably unnecessary. but could come in handy in case there's some delays (or race condition) in
            //marking the job as complete via the storage provider and the job gets picked up for execution in the meantime.
            while (DateTime.Now.Subtract(startTime).TotalMilliseconds <= 10000)
            {
                if (cts is not null) //job execution has started and cts is available
                {
                    if (!cts.IsCancellationRequested)
                        cts.Cancel(false);

                    return; //we're done!
                }
                await Task.Delay(1000);
            }
        }
    }

    async Task CommandExecutorTask()
    {
        while (!_appCancellation.IsCancellationRequested)
        {
            IEnumerable<TStorageRecord> records;

            try
            {
                records = await _storage.GetNextBatchAsync(
                              new()
                              {
                                  Limit = _parallelOptions.MaxDegreeOfParallelism,
                                  QueueID = QueueID,
                                  CancellationToken = _appCancellation,
                                  Match = r => r.QueueID == QueueID &&
                                               r.IsComplete == false &&
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
                // We must periodically reevaluate the storage status to ascertain if the user has rescheduled any previous jobs while no new jobs are being queued.
                // Failure to conduct this periodic check could result in rescheduled jobs only executing upon the arrival of new jobs, potentially leading to expired job executions.
                await (_isInUse
                           ? Task.WhenAny(_sem.WaitAsync(_appCancellation), Task.Delay(_semWaitLimit))
                           : _sem.WaitAsync(_appCancellation));
            }
            else
                await Parallel.ForEachAsync(records, _parallelOptions, ExecuteCommand);
        }

        async ValueTask ExecuteCommand(TStorageRecord record, CancellationToken _)
        {
            using var cts = _cancellations.GetOrCreate<CancellationTokenSource?>(
                record.TrackingID,
                e =>
                {
                    e.AbsoluteExpirationRelativeToNow = _cancellationExpiry;
                    var s = new CancellationTokenSource(_executionTimeLimit);

                    return s;
                });

            if (cts is null) //don't execute this job because cancellation has been requested already
                return;      //the cts will be null if the entry was created by a call to CancelJobAsync() before the job was picked up for execution

            //if cts is not null, proceed with job execution as cancellation has not been requested yet.

            try
            {
                var cmd = record.GetCommand<TCommand>();
                record.Command = cmd; //needed in case user does whole record (non-partial) updates via storage provider.
                await cmd.ExecuteAsync(cts.Token);
                _cancellations.Remove(record.TrackingID); //remove entry on completion. cancellations are not possible/valid after this point.
            }
            catch (Exception x)
            {
                _cancellations.Remove(record.TrackingID); //remove entry on execution error to allow obtaining a new cts on retry/reentry
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


abstract class JobQueueWithReturnValue<TResult> : JobQueueBase
{

    protected abstract Task<Guid> StoreJobAsync(ICommand<TResult> command, DateTime? executeAfter, DateTime? expireOn, CancellationToken ct);
    protected abstract Task<TResult?> GetJobResult(Guid trackingId, CancellationToken ct);

    internal static Task<Guid> AddToQueueAsync(ICommand<TResult> command, DateTime? executeAfter, DateTime? expireOn, CancellationToken ct)
    {
        if (executeAfter?.Kind is not null and not DateTimeKind.Utc ||
            expireOn?.Kind is not null and not DateTimeKind.Utc)
            throw new ArgumentException($"Only UTC dates are accepted for '{nameof(executeAfter)}' & '{nameof(expireOn)}' parameters!");

        var tCommand = command.GetType();

        return
            !JobQueues.TryGetValue(tCommand, out var queue)
                ? throw new InvalidOperationException($"A job queue has not been registered for [{tCommand.FullName}]")
                : queue is JobQueueWithReturnValue<TResult> queueWithReturnValue
                    ? queueWithReturnValue.StoreJobAsync(command, executeAfter, expireOn, ct)
                    : throw new InvalidOperationException($"A job queue with return value [{typeof(TResult).FullName}] has not been registered for [{tCommand.FullName}]");
    }

    internal static Task CancelJobAsync<TCommand>(Guid trackingId, CancellationToken ct) where TCommand : ICommand<TResult>
    {
        var tCommand = typeof(TCommand);

        return
            !JobQueues.TryGetValue(tCommand, out var queue)
                ? throw new InvalidOperationException($"A job queue has not been registered for [{tCommand.FullName}]")
                : queue.CancelJobAsync(trackingId, ct);
    }

    internal static Task<TResult?> GetJobResultAsync<TCommand>(Guid trackingId, CancellationToken ct) where TCommand : ICommand<TResult>
    {
        var tCommand = typeof(TCommand);

        return
            !JobQueues.TryGetValue(tCommand, out var queue)
                ? throw new InvalidOperationException($"A job queue has not been registered for [{tCommand.FullName}]")
                : queue is JobQueueWithReturnValue<TResult> queueWithReturnValue
                    ? queueWithReturnValue.GetJobResult(trackingId, ct)
                    : throw new InvalidOperationException($"A job queue with return value [{typeof(TResult).FullName}] has not been registered for [{tCommand.FullName}]");
    }
}

// created by DI as singleton
// this is a copy of above code
sealed class JobQueue<TCommand, TResult, TStorageRecord, TStorageProvider> : JobQueueWithReturnValue<TResult>
    where TCommand : ICommand<TResult>
    where TStorageRecord : IJobStorageRecord, new()
    where TStorageProvider : IJobStorageProvider<TStorageRecord>
{
    static readonly Type _tCommand = typeof(TCommand);
    static readonly string _tCommandName = _tCommand.FullName!;

    //public due to: https://github.com/FastEndpoints/FastEndpoints/issues/468
    public static readonly string QueueID = _tCommandName.ToHash();

    readonly MemoryCache _cancellations = new(new MemoryCacheOptions());
    readonly ParallelOptions _parallelOptions = new() { MaxDegreeOfParallelism = Environment.ProcessorCount };
    readonly CancellationToken _appCancellation;
    readonly TStorageProvider _storage;
    readonly SemaphoreSlim _sem = new(0);
    readonly ILogger _log;
    TimeSpan _executionTimeLimit;
    TimeSpan _cancellationExpiry;
    TimeSpan _semWaitLimit;
    bool _isInUse;

    public JobQueue(TStorageProvider storageProvider,
                    IHostApplicationLifetime appLife,
                    ILogger<JobQueue<TCommand, TResult, TStorageRecord, TStorageProvider>> logger)
    {
        JobQueues[_tCommand] = this;
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
        _cancellationExpiry = executionTimeLimit + TimeSpan.FromHours(1);
        _semWaitLimit = semWaitLimit;
        _ = CommandExecutorTask();
    }

    protected override async Task<Guid> StoreJobAsync(ICommand<TResult> command, DateTime? executeAfter, DateTime? expireOn, CancellationToken ct)
    {
        _isInUse = true;
        var job = new TStorageRecord
        {
            TrackingID = Guid.NewGuid(),
            QueueID = QueueID,
            ExecuteAfter = executeAfter ?? DateTime.UtcNow,
            ExpireOn = expireOn ?? DateTime.UtcNow.AddHours(4)
        };
        job.SetCommand<TCommand,TResult>((TCommand)command);
        await _storage.StoreJobAsync(job, ct);
        _sem.Release();

        return job.TrackingID;
    }

    protected override async Task<TResult?> GetJobResult(Guid trackingId, CancellationToken ct)
    {
        var job = await _storage.GetJob(
                      new()
                      {
                          TrackingID = trackingId,
                          CancellationToken = _appCancellation,
                          Match = r => r.QueueID == QueueID &&
                                       r.TrackingID == trackingId &&
                                       r.IsComplete == true
                      });

        return job is null ? default(TResult?) : job.GetResult<TCommand, TResult>();
    }

    protected internal override async Task CancelJobAsync(Guid trackingId, CancellationToken ct)
    {
        _ = AttemptCancellationTask(trackingId, _cancellations, _cancellationExpiry);
        await _storage.CancelJobAsync(trackingId, ct);

        static async Task AttemptCancellationTask(Guid trackingId, MemoryCache cancellations, TimeSpan cancelExpiry)
        {
            var cts = cancellations.GetOrCreate<CancellationTokenSource?>(
                trackingId,
                e =>
                {
                    e.SlidingExpiration = cancelExpiry;

                    return null;
                });

            var startTime = DateTime.Now;

            //this is probably unnecessary. but could come in handy in case there's some delays (or race condition) in
            //marking the job as complete via the storage provider and the job gets picked up for execution in the meantime.
            while (DateTime.Now.Subtract(startTime).TotalMilliseconds <= 10000)
            {
                if (cts is not null) //job execution has started and cts is available
                {
                    if (!cts.IsCancellationRequested)
                        cts.Cancel(false);

                    return; //we're done!
                }
                await Task.Delay(1000);
            }
        }
    }

    async Task CommandExecutorTask()
    {
        while (!_appCancellation.IsCancellationRequested)
        {
            IEnumerable<TStorageRecord> records;

            try
            {
                records = await _storage.GetNextBatchAsync(
                              new()
                              {
                                  Limit = _parallelOptions.MaxDegreeOfParallelism,
                                  QueueID = QueueID,
                                  CancellationToken = _appCancellation,
                                  Match = r => r.QueueID == QueueID &&
                                               r.IsComplete == false &&
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
                // We must periodically reevaluate the storage status to ascertain if the user has rescheduled any previous jobs while no new jobs are being queued.
                // Failure to conduct this periodic check could result in rescheduled jobs only executing upon the arrival of new jobs, potentially leading to expired job executions.
                await (_isInUse
                           ? Task.WhenAny(_sem.WaitAsync(_appCancellation), Task.Delay(_semWaitLimit))
                           : _sem.WaitAsync(_appCancellation));
            }
            else
                await Parallel.ForEachAsync(records, _parallelOptions, ExecuteCommand);
        }

        async ValueTask ExecuteCommand(TStorageRecord record, CancellationToken _)
        {
            using var cts = _cancellations.GetOrCreate<CancellationTokenSource?>(
                record.TrackingID,
                e =>
                {
                    e.AbsoluteExpirationRelativeToNow = _cancellationExpiry;
                    var s = new CancellationTokenSource(_executionTimeLimit);

                    return s;
                });

            if (cts is null) //don't execute this job because cancellation has been requested already
                return;      //the cts will be null if the entry was created by a call to CancelJobAsync() before the job was picked up for execution

            //if cts is not null, proceed with job execution as cancellation has not been requested yet.

            try
            {
                var cmd = record.GetCommand<TCommand, TResult>();
                record.Command = cmd; //needed in case user does whole record (non-partial) updates via storage provider.
                var result = await cmd.ExecuteAsync(cts.Token);
                record.SetResult<TCommand, TResult>(result);
                _cancellations.Remove(record.TrackingID); //remove entry on completion. cancellations are not possible/valid after this point.
            }
            catch (Exception x)
            {
                _cancellations.Remove(record.TrackingID); //remove entry on execution error to allow obtaining a new cts on retry/reentry
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