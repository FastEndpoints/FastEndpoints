using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FastEndpoints.JobsQueues;

namespace FastEndpoints;

[UnconditionalSuppressMessage("aot", "IL2090")]
abstract class JobQueueBase
{
    //key: tCommand
    //val: job queue for the command type
    //values get created when the DI container resolves each job queue type and the ctor is run.
    //see ctor in JobQueue<TCommand, TStorageRecord, TStorageProvider>
    protected static readonly ConcurrentDictionary<Type, JobQueueBase> JobQueues = new();

    protected abstract IJobStorageRecord CreateJob(ICommandBase command, DateTime? executeAfter, DateTime? expireOn);

    protected abstract void TriggerJob();

    protected abstract Task<Guid> StoreJobAsync(ICommandBase command, DateTime? executeAfter, DateTime? expireOn, CancellationToken ct);

    protected abstract Task CancelJobAsync(Guid trackingId, CancellationToken ct);

    protected abstract Task<TResult?> GetJobResultAsync<TResult>(Guid trackingId, CancellationToken ct);

    protected abstract Task StoreJobResultAsync<TResult>(Guid trackingId, TResult result, CancellationToken ct);

    internal abstract void SetLimits(int concurrencyLimit, TimeSpan executionTimeLimit, TimeSpan semWaitLimit);

    internal static TStorageRecord CreateJob<TStorageRecord>(ICommandBase command, DateTime? executeAfter, DateTime? expireOn)
        where TStorageRecord : class, IJobStorageRecord, new()
    {
        var tCommand = command.GetType();

        return !JobQueues.TryGetValue(tCommand, out var queue)
                   ? throw new InvalidOperationException($"A job queue has not been registered for [{tCommand.FullName}]")
                   : (TStorageRecord)queue.CreateJob(command, executeAfter, expireOn);
    }

    internal static void TriggerJobExecution(Type commandType)
    {
        if (!JobQueues.TryGetValue(commandType, out var queue))
            throw new InvalidOperationException($"A job queue has not been registered for [{commandType.FullName}]");

        queue.TriggerJob();
    }

    internal static Task<Guid> AddToQueueAsync(ICommandBase command, DateTime? executeAfter, DateTime? expireOn, CancellationToken ct)
    {
        var tCommand = command.GetType();

        return !JobQueues.TryGetValue(tCommand, out var queue)
                   ? throw new InvalidOperationException($"A job queue has not been registered for [{tCommand.FullName}]")
                   : queue.StoreJobAsync(command, executeAfter, expireOn, ct);
    }

    internal static Task CancelJobAsync<TCommand>(Guid trackingId, CancellationToken ct) where TCommand : ICommandBase
    {
        var tCommand = typeof(TCommand);

        return !JobQueues.TryGetValue(tCommand, out var queue)
                   ? throw new InvalidOperationException($"A job queue has not been registered for [{tCommand.FullName}]")
                   : queue.CancelJobAsync(trackingId, ct);
    }

    internal static Task<TResult?> GetJobResultAsync<TCommand, TResult>(Guid trackingId, CancellationToken ct) where TCommand : ICommandBase
    {
        var tCommand = typeof(TCommand);
        var tResult = tCommand.GetInterface(typeof(ICommand<>).Name)?.GetGenericArguments()[0];

        if (tResult == Types.VoidResult)
            throw new InvalidOperationException($"Job results are not supported with commands that don't return a result! Offending command: [{tCommand.FullName}]");

        return !JobQueues.TryGetValue(tCommand, out var queue)
                   ? throw new InvalidOperationException($"A job queue has not been registered for [{tCommand.FullName}]")
                   : queue.GetJobResultAsync<TResult>(trackingId, ct);
    }

    internal static Task StoreJobResultAsync<TCommand, TResult>(Guid trackingId, TResult result, CancellationToken ct)
        where TCommand : ICommandBase
        where TResult : IJobResult
    {
        var tCommand = typeof(TCommand);
        var tResult = tCommand.GetInterface(typeof(ICommand<>).Name)?.GetGenericArguments()[0];

        if (tResult == Types.VoidResult)
            throw new InvalidOperationException($"Job results are not supported with commands that don't return a result! Offending command: [{tCommand.FullName}]");

        return
            !JobQueues.TryGetValue(tCommand, out var queue)
                ? throw new InvalidOperationException($"A job queue has not been registered for [{tCommand.FullName}]")
                : queue.StoreJobResultAsync(trackingId, result, ct);
    }
}

// instantiated by DI as singleton
[SuppressMessage("Reliability", "CA2016:Forward the \'CancellationToken\' parameter to methods"),
 SuppressMessage("ReSharper", "MethodSupportsCancellation"),
 SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
sealed class JobQueue<TCommand, TResult, TStorageRecord, TStorageProvider> : JobQueueBase
    where TCommand : class, ICommandBase
    where TStorageRecord : class, IJobStorageRecord, new()
    where TStorageProvider : class, IJobStorageProvider<TStorageRecord>
{
    static readonly Type _tCommand = typeof(TCommand);
    static readonly string _tCommandName = _tCommand.FullName!;

    //public due to: https://github.com/FastEndpoints/FastEndpoints/issues/468
    public static readonly string QueueID = _tCommandName.ToHash();

    readonly ConcurrentDictionary<Guid, CancellationTokenSource?> _cancellations = new();
    readonly ParallelOptions _parallelOptions = new() { MaxDegreeOfParallelism = Environment.ProcessorCount };
    readonly CancellationToken _appCancellation;
    readonly TStorageProvider _storage;
    readonly IJobResultProvider? _resultStorage;
    readonly SemaphoreSlim _sem = new(0);
    readonly ILogger _log;
    bool _activated; // when false, the executor blocks indefinitely on the semaphore (no DB polling)
    TimeSpan _executionTimeLimit;
    TimeSpan _semWaitLimit;
    DateTimeOffset _nextCleanupOn;

    public JobQueue(TStorageProvider storageProvider,
                    IHostApplicationLifetime appLife,
                    ILogger<JobQueue<TCommand, TResult, TStorageRecord, TStorageProvider>> logger)
    {
        JobQueues[_tCommand] = this;
        _storage = storageProvider;
        _resultStorage = storageProvider as IJobResultProvider;
        _activated = storageProvider.DistributedJobProcessingEnabled;
        _appCancellation = appLife.ApplicationStopping;
        _parallelOptions.CancellationToken = _appCancellation;
        _log = logger;
        _nextCleanupOn = DateTime.UtcNow.AddMinutes(5);
        JobStorage<TStorageRecord, TStorageProvider>.Provider = _storage;
        JobStorage<TStorageRecord, TStorageProvider>.AppCancellation = _appCancellation;
        JobStorage<TStorageRecord, TStorageProvider>.Logger = _log;
        JobStorage<TStorageRecord, TStorageProvider>.StartStaleJobPurging();
    }

    internal override void SetLimits(int concurrencyLimit, TimeSpan executionTimeLimit, TimeSpan semWaitLimit)
    {
        _parallelOptions.MaxDegreeOfParallelism = concurrencyLimit;
        _executionTimeLimit = executionTimeLimit;
        _semWaitLimit = semWaitLimit;
        _ = CommandExecutorTask();
    }

    protected override TStorageRecord CreateJob(ICommandBase command, DateTime? executeAfter, DateTime? expireOn)
    {
        if (executeAfter?.Kind is not null and not DateTimeKind.Utc ||
            expireOn?.Kind is not null and not DateTimeKind.Utc)
            throw new ArgumentException($"Only UTC dates are accepted for '{nameof(executeAfter)}' & '{nameof(expireOn)}' parameters!");

        var now = DateTime.UtcNow; //capture current time to avoid discrepancies
        var job = new TStorageRecord
        {
            TrackingID = Guid.NewGuid(),
            QueueID = QueueID,
            ExecuteAfter = executeAfter ?? now,
            ExpireOn = expireOn ?? now.AddHours(4)
        };

        if (job is IHasCommandType jct)
            jct.CommandType = _tCommandName;

        if (command is IHasTrackingID cti)
            cti.TrackingID = job.TrackingID;

        job.SetCommand((TCommand)command);

        return job;
    }

    protected override void TriggerJob()
    {
        _activated = true;
        _sem.Release();
    }

    protected override async Task<Guid> StoreJobAsync(ICommandBase command, DateTime? executeAfter, DateTime? expireOn, CancellationToken ct)
    {
        var job = CreateJob(command, executeAfter, expireOn);
        await _storage.StoreJobAsync(job, ct);
        TriggerJob();

        return job.TrackingID;
    }

    protected override Task<TRes?> GetJobResultAsync<TRes>(Guid trackingId, CancellationToken ct) where TRes : default
    {
        if (_storage is not IJobResultProvider s)
            throw new NotSupportedException($"Please implement the  interface '{nameof(IJobResultProvider)}' on the job storage provider!");

        var tRes = typeof(TRes);
        var tResult = typeof(TResult);
        var cmdName = typeof(TCommand).FullName;

        if (tRes != tResult)
            throw new InvalidOperationException($"The correct result type for the command [{cmdName}] should be: [{tResult.FullName}]! You specified: [{tRes.FullName}]!");

        return s.GetJobResultAsync<TRes>(trackingId, ct);
    }

    protected override Task StoreJobResultAsync<TRes>(Guid trackingId, TRes result, CancellationToken ct) where TRes : default
    {
        if (_storage is not IJobResultProvider s)
            throw new NotSupportedException($"Please implement the  interface '{nameof(IJobResultProvider)}' on the job storage provider!");

        var tRes = typeof(TRes);
        var tResult = typeof(TResult);
        var cmdName = typeof(TCommand).FullName;

        if (tRes != tResult)
            throw new InvalidOperationException($"The correct result type for the command [{cmdName}] should be: [{tResult.FullName}]! You specified: [{tRes.FullName}]!");

        return s.StoreJobResultAsync(trackingId, result, ct);
    }

    protected override async Task CancelJobAsync(Guid trackingId, CancellationToken ct)
    {
        // if job is executing, fetch the cts added by ExecuteCommand, else store an already canceled cts
        var cts = _cancellations.GetOrAdd(trackingId, static _ => new(0));

        if (cts is null)
            return; // job is already marked canceled in storage

        // job execution has started and cts is available
        try
        {
            if (!cts.IsCancellationRequested)
                cts.Cancel(false);
        }
        catch (ObjectDisposedException) { }

        // set null as the mark for dictionary cleanup and dispose the cts
        if (_cancellations.TryUpdate(trackingId, null, cts))
        {
            try { cts.Dispose(); }
            catch (ObjectDisposedException) { }
        }

        // ReSharper disable once PossiblyMistakenUseOfCancellationToken
        await _storage.CancelJobAsync(trackingId, ct);
    }

    async Task CommandExecutorTask()
    {
        while (!_appCancellation.IsCancellationRequested)
        {
            ICollection<TStorageRecord> records;
            int fetchedCount;

            try
            {
                // only becomes 'true' in non-distributed first iteration making 'ExecuteAfter <= now' in the Match filter ineffective, causing future jobs to get fetched as well.
                // when in distributed mode, all instances must periodically poll to ensure jobs created by other instances are picked up.
                var isProbe = !_activated;

                // capture once so providers that translate predicates can parameterize it cleanly
                var now = DateTime.UtcNow;

                records = await _storage.GetNextBatchAsync(
                              new()
                              {
                                  Limit = _parallelOptions.MaxDegreeOfParallelism,
                                  QueueID = QueueID,
                                  CancellationToken = _appCancellation,
                                  ExecutionTimeLimit = _executionTimeLimit,
                                  Match = r => r.QueueID == QueueID &&
                                               !r.IsComplete &&
                                               (isProbe || r.ExecuteAfter <= now) &&
                                               r.ExpireOn >= now &&
                                               r.DequeueAfter <= now
                              });

                fetchedCount = records.Count;

                if (isProbe)
                {
                    if (fetchedCount > 0) // eligible + future jobs found during probe
                        _activated = true;

                    records = records.Where(r => r.ExecuteAfter <= now).ToArray(); // filter out future jobs
                }
            }
            catch (OperationCanceledException) when (_appCancellation.IsCancellationRequested)
            {
                break; // no need to log/retry if app is being shutdown
            }
            catch (Exception x)
            {
                _log.StorageRetrieveError(QueueID, _tCommandName, x.Message);
                await Task.Delay(5000);

                continue;
            }

            try
            {
                if (records.Count > 0)
                {
                    _activated = true;
                    await Parallel.ForEachAsync(records, _parallelOptions, ExecuteCommand);
                }

                if (fetchedCount < _parallelOptions.MaxDegreeOfParallelism)
                {
                    // when activated (distributed mode, or jobs have been found), wait with a timeout so we periodically re-check for future jobs
                    // becoming due or jobs added by other distributed workers.
                    // when not activated (non-distributed with no jobs found yet), block indefinitely to avoid pointless DB polling.
                    // TriggerJob() sets _activated=true and releases the semaphore when the first job is queued.
                    await _sem.WaitAsync(_activated ? _semWaitLimit : Timeout.InfiniteTimeSpan, _appCancellation);
                }
            }
            catch (OperationCanceledException) when (_appCancellation.IsCancellationRequested)
            {
                break; // exit immediately if app is being shutdown
            }
            catch (Exception x)
            {
                // this would typically never be hit because ExecuteCommand handles exceptions internally.
                // only here as a safety net to prevent the executor loop from crashing.
                _log.CommandParallelExecutionWarning(_tCommandName, x.Message);
                await Task.Delay(5000);

                continue;
            }

            // else there are more records than the page size, so continue next iteration

            // cleanup any cancellations that have been marked canceled in storage
            if (DateTime.UtcNow >= _nextCleanupOn)
            {
                foreach (var kv in _cancellations)
                {
                    if (kv.Value is null)
                        _cancellations.TryRemove(kv.Key, out _);
                }

                _nextCleanupOn = DateTime.UtcNow.AddMinutes(5);
            }

            // ReSharper disable once MethodHasAsyncOverloadWithCancellation
            // reset/drain _sem CurrentCount to 0 in case multiple releases happened
            // passing app cancellation here is not needed as it's an immediate return.
            while (_sem.Wait(0)) { }
        }

        async ValueTask ExecuteCommand(TStorageRecord record, CancellationToken _)
        {
            // ReSharper disable once HeapView.CanAvoidClosure
            using var cts = _cancellations.GetOrAdd(
                record.TrackingID,
                _ =>
                {
                    var s = CancellationTokenSource.CreateLinkedTokenSource(_appCancellation);
                    s.CancelAfter(_executionTimeLimit);

                    return s;
                });

            // don't execute this job because cancellation has been requested already
            // the cts will be null or already canceled if the entry was created by a call to CancelJobAsync() before the job was picked up for execution
            if (cts is null || cts.IsCancellationRequested)
                return;

            //if cts is not null/canceled, proceed with job execution as cancellation has not been requested yet.
            try
            {
                var cmd = record.GetCommand<TCommand>();
                record.Command = cmd; //needed in case user does whole record (non-partial) updates via storage provider.

                switch (cmd)
                {
                    case ICommand c:
                        await c.ExecuteAsync(cts.Token);

                        break;
                    case ICommand<TResult> cr:
                        ((IJobResultStorage)record).SetResult(await cr.ExecuteAsync(cts.Token));

                        break;
                }

                _cancellations.TryRemove(record.TrackingID, out var _); // remove entry on completion. cancellations are not possible/valid after this point.
            }
            catch (Exception x) when (x is OperationCanceledException && IsManuallyCancelled())
            {
                // don't treat as a handler execution failure when manually canceled mid-execution
                _cancellations.TryRemove(record.TrackingID, out var _);
                _log.JobCancelledManually(_tCommandName, record.TrackingID);

                return;
            }
            catch (Exception x)
            {
                _cancellations.TryRemove(record.TrackingID, out var _); // remove entry on execution error to allow obtaining a new cts on retry/reentry
                _log.CommandExecutionCritical(_tCommandName, x.Message);

                while (true)
                {
                    try
                    {
                        if (_resultStorage is not null)
                            (record as IJobResultStorage)?.SetResult(await GetJobResultAsync<TResult>(record.TrackingID, _appCancellation));

                        await _storage.OnHandlerExecutionFailureAsync(record, x, _appCancellation);

                        break;
                    }
                    catch (Exception xx)
                    {
                        _log.StorageOnExecutionFailureError(QueueID, _tCommandName, xx.Message);

                        if (_appCancellation.IsCancellationRequested || IsJobCancelled())
                            break;

                        await Task.Delay(5000);
                    }
                }

                return; //abort execution here
            }

            while (true)
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

                    if (_appCancellation.IsCancellationRequested || IsJobCancelled())
                        break;

                    await Task.Delay(5000);
                }
            }

            while (_resultStorage is not null)
            {
                try
                {
                    await _resultStorage.StoreJobResultAsync(
                        record.TrackingID,
                        ((IJobResultStorage)record).GetResult<TResult>(),
                        _appCancellation);

                    break;
                }
                catch (Exception x)
                {
                    _log.StorageStoreJobResultError(QueueID, _tCommandName, x.Message);

                    if (_appCancellation.IsCancellationRequested || IsJobCancelled())
                        break;

                    await Task.Delay(5000);
                }
            }

            // checks if the job was manually canceled via CancelJobAsync().
            // CancelJobAsync sets the _cancellations entry to null after cancelling.
            // this is used to distinguish manual cancellation from execution time limit expiry or app shutdown.
            bool IsManuallyCancelled()
                => !_appCancellation.IsCancellationRequested && _cancellations.TryGetValue(record.TrackingID, out var c) && c is null;

            bool IsJobCancelled()
            {
                try
                {
                    if (cts.IsCancellationRequested)
                        return true;
                }
                catch (ObjectDisposedException)
                {
                    return true;
                }

                if (!_cancellations.TryGetValue(record.TrackingID, out var c))
                    return false;

                if (c is null)
                    return true;

                try
                {
                    if (c.IsCancellationRequested)
                        return true;
                }
                catch (ObjectDisposedException)
                {
                    return true;
                }

                return false;
            }
        }
    }
}