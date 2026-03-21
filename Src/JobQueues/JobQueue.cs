using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using FastEndpoints.JobsQueues;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

    internal abstract void SetLimits(int concurrencyLimit, TimeSpan executionTimeLimit, TimeSpan semWaitLimit, TimeSpan? retryDelay = null);

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
    static readonly string _commandTypeName = _tCommand.FullName!;

    public static readonly string QueueID = _commandTypeName.ToHash(); //public due to: https://github.com/FastEndpoints/FastEndpoints/issues/468
    internal Task ExecutorTask { get; private set; } = Task.CompletedTask;

    readonly ConcurrentDictionary<Guid, CancellationTokenSource?> _cancellations = new();
    readonly CancellationToken _appCancellation;
    readonly TStorageProvider _storage;
    readonly IJobResultProvider? _resultStorage;
    readonly SemaphoreSlim _sem = new(0);
    readonly ILogger _log;
    readonly bool _isDistributed;
    bool _activated; // when false, the executor blocks indefinitely on the semaphore (no DB polling)
    int _maxConcurrency = Environment.ProcessorCount;
    TimeSpan _executionTimeLimit;
    TimeSpan _semWaitLimit;
    TimeSpan _retryDelay = TimeSpan.FromSeconds(5);
    DateTime? _nextCleanupOn;

    public JobQueue(TStorageProvider storageProvider, IHostApplicationLifetime appLife, ILogger<JobQueue<TCommand, TResult, TStorageRecord, TStorageProvider>> logger)
    {
        JobQueues[_tCommand] = this;
        _storage = storageProvider;
        _resultStorage = storageProvider as IJobResultProvider;
        _isDistributed = storageProvider.DistributedJobProcessingEnabled;
        _activated = _isDistributed;
        _appCancellation = appLife.ApplicationStopping;
        _log = logger;
        JobStorage<TStorageRecord, TStorageProvider>.Provider = _storage;
        JobStorage<TStorageRecord, TStorageProvider>.AppCancellation = _appCancellation;
        JobStorage<TStorageRecord, TStorageProvider>.Logger = _log;
        JobStorage<TStorageRecord, TStorageProvider>.StartStaleJobPurging();
    }

    internal override void SetLimits(int concurrencyLimit, TimeSpan executionTimeLimit, TimeSpan semWaitLimit, TimeSpan? retryDelay = null)
    {
        _maxConcurrency = concurrencyLimit;
        _executionTimeLimit = executionTimeLimit;
        _semWaitLimit = semWaitLimit;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(5);
        ExecutorTask = CommandExecutorTask();
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
            jct.CommandType = _commandTypeName;

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
        var cts = GetOrAddCancellation(trackingId, static () => new(0));

        if (cts is null)
        {
            await _storage.CancelJobAsync(trackingId, ct); // retry persisting manual cancellation if a previous attempt failed or is still in flight

            return;
        }

        bool rollbackManualMarkerOnStorageFailure;

        try
        {
            rollbackManualMarkerOnStorageFailure = cts.IsCancellationRequested;
        }
        catch (ObjectDisposedException)
        {
            rollbackManualMarkerOnStorageFailure = true;
        }

        // mark manual cancellation before signaling the token so ExecuteCommand can
        // distinguish a user initiated cancellation from timeouts/app shutdown.
        var markedForManualCancellation = _cancellations.TryUpdate(trackingId, null, cts);

        if (markedForManualCancellation)
            _nextCleanupOn ??= DateTime.UtcNow.AddMinutes(5); // if marked null, schedule next dictionary cleanup in 5 minutes

        if (markedForManualCancellation)
        {
            try
            {
                if (!cts.IsCancellationRequested)
                    cts.Cancel(false);
            }
            catch (ObjectDisposedException) { }
        }

        try
        {
            await _storage.CancelJobAsync(trackingId, ct);
        }
        catch
        {
            if (markedForManualCancellation && rollbackManualMarkerOnStorageFailure)
                _cancellations.TryRemove(trackingId, out _);

            throw;
        }
    }

    async Task CommandExecutorTask()
    {
        var executions = new Dictionary<Guid, Task>();

        while (!_appCancellation.IsCancellationRequested)
        {
            await ObserveCompletedExecutions();

            CleanupStaleManualCancellationMarkers();

            if (executions.Count < _maxConcurrency)
            {
                var availableSlots = _maxConcurrency - executions.Count;
                ICollection<TStorageRecord> records;

                try
                {
                    // only becomes 'true' in non-distributed first iteration making 'ExecuteAfter <= now' in the Match filter ineffective, causing future jobs to get fetched as well.
                    // when in distributed mode, all instances must periodically poll to ensure jobs created by other instances are picked up.
                    var isProbe = !_activated;

                    // capture once so providers that translate predicates can parameterize it cleanly
                    var now = DateTime.UtcNow;

                    Expression<Func<TStorageRecord, bool>> matchExpr = _isDistributed
                                                                           ? r => r.QueueID == QueueID &&
                                                                                  !r.IsComplete &&
                                                                                  (isProbe || r.ExecuteAfter <= now) &&
                                                                                  r.ExpireOn >= now &&
                                                                                  r.DequeueAfter <= now
                                                                           : r => r.QueueID == QueueID &&
                                                                                  !r.IsComplete &&
                                                                                  (isProbe || r.ExecuteAfter <= now) &&
                                                                                  r.ExpireOn >= now;
                    records = await _storage.GetNextBatchAsync(
                                  new()
                                  {
                                      Limit = _isDistributed ? availableSlots : _maxConcurrency, //distributed provider already filters out in-flight(already leased) jobs.
                                      QueueID = QueueID,
                                      CancellationToken = _appCancellation,
                                      ExecutionTimeLimit = _executionTimeLimit,
                                      Match = matchExpr
                                  });

                    if (isProbe)
                    {
                        if (records.Count > 0) // eligible + future jobs found during probe
                            _activated = true;
                    }

                    if (isProbe || executions.Count > 0)
                    {
                        IEnumerable<TStorageRecord> filtered = records;

                        if (isProbe)
                            filtered = filtered.Where(r => r.ExecuteAfter <= now); // filter out future jobs

                        if (executions.Count > 0)
                            filtered = filtered.Where(r => !executions.ContainsKey(r.TrackingID)); // filter out in-flight jobs

                        if (!ReferenceEquals(filtered, records))
                            records = filtered.ToArray();
                    }
                }
                catch (OperationCanceledException) when (_appCancellation.IsCancellationRequested)
                {
                    break; // no need to log/retry if app is being shutdown
                }
                catch (Exception x)
                {
                    _log.StorageRetrieveError(QueueID, _commandTypeName, x.Message);
                    await Task.Delay(_retryDelay);

                    continue;
                }

                if (records.Count > 0)
                {
                    _activated = true;

                    foreach (var record in records.Take(availableSlots))
                        executions[record.TrackingID] = ExecuteCommand(record);

                    if (executions.Count == _maxConcurrency)
                        continue;
                }

                await WaitForSignalAsync();

                continue;
            }

            //no free slots available. wait for at least one slot to free up, and honor app shutdown.
            try
            {
                await Task.WhenAny(executions.Values).WaitAsync(_appCancellation);
            }
            catch (OperationCanceledException) when (_appCancellation.IsCancellationRequested)
            {
                break; // exit immediately if app is being shutdown
            }
            catch (Exception x)
            {
                _log.CommandParallelExecutionWarning(_commandTypeName, x.Message);
                await Task.Delay(_retryDelay);
            }
        }

        await DrainExecutionsAsync();

        async Task ExecuteCommand(TStorageRecord record)
        {
            // ReSharper disable once HeapView.CanAvoidClosure
            using var cts = GetOrAddCancellation(
                record.TrackingID,
                () =>
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
                        var result = await cr.ExecuteAsync(cts.Token);
                        if (record is IJobResultStorage rec)
                            rec.SetResult(result);

                        break;
                }

                _cancellations.TryRemove(record.TrackingID, out _); // remove entry on completion. cancellations are not possible/valid after this point.
            }
            catch (Exception x) when (x is OperationCanceledException && IsManuallyCancelled())
            {
                // don't treat as a handler execution failure when manually canceled mid-execution
                _cancellations.TryRemove(record.TrackingID, out _);
                _log.JobCancelledManually(_commandTypeName, record.TrackingID);

                return;
            }
            catch (Exception x)
            {
                _cancellations.TryRemove(record.TrackingID, out _); // remove entry on execution error to allow obtaining a new cts on retry/reentry
                _log.CommandExecutionCritical(_commandTypeName, x.Message);

                while (true)
                {
                    try
                    {
                        // fetch the last known result in case the job handler stored intermediate results before failing, so it can be passed to OnHandlerExecutionFailureAsync
                        if (_resultStorage is not null)
                            (record as IJobResultStorage)?.SetResult(await GetJobResultAsync<TResult>(record.TrackingID, CancellationToken.None));

                        await _storage.OnHandlerExecutionFailureAsync(record, x, CancellationToken.None);

                        break;

                        // WARNING: if _appCancellation is used above (instead of CancellationToken.None),
                        //          ORMs could throw OperationCanceledException without actually executing DB operations during app shutdown.
                    }
                    catch (Exception xx)
                    {
                        _log.StorageOnExecutionFailureError(QueueID, _commandTypeName, xx.Message);

                        if (_appCancellation.IsCancellationRequested || IsManuallyCancelled())
                            break;

                        await Task.Delay(_retryDelay);
                    }
                }

                return; //abort execution here
            }

            while (_resultStorage is not null && record is IJobResultStorage rec)
            {
                try
                {
                    // if _appCancellation is used here, ORMs could throw without executing the store operation during app shutdown, causing result to get lost.
                    await _resultStorage.StoreJobResultAsync(record.TrackingID, rec.GetResult<TResult>(), CancellationToken.None);

                    break;
                }
                catch (Exception x)
                {
                    _log.StorageStoreJobResultError(QueueID, _commandTypeName, x.Message);

                    if (_appCancellation.IsCancellationRequested)
                        break; // losing the result is an acceptable risk if app is shutting down. do not 'return;' here (which causes re-execution of job).

                    await Task.Delay(_retryDelay);
                }
            }

            while (true)
            {
                try
                {
                    record.IsComplete = true;

                    // if _appCancellation is used, ORMs could throw OperationCanceledException without actually executing DB operations during app shutdown.
                    await _storage.MarkJobAsCompleteAsync(record, CancellationToken.None);

                    break;
                }
                catch (Exception x)
                {
                    _log.StorageMarkAsCompleteError(QueueID, _commandTypeName, x.Message);

                    if (_appCancellation.IsCancellationRequested)
                        break;

                    await Task.Delay(_retryDelay);
                }
            }

            // checks if the job was manually canceled via CancelJobAsync().
            // CancelJobAsync sets the _cancellations entry to null before cancelling.
            // this is used to distinguish manual cancellation from execution time limit expiry or app shutdown.
            bool IsManuallyCancelled()
                => !_appCancellation.IsCancellationRequested && _cancellations.TryGetValue(record.TrackingID, out var c) && c is null;
        }

        async Task WaitForSignalAsync()
        {
            try
            {
                // when activated (distributed mode, or jobs have been found), wait with a timeout so we periodically re-check for future jobs
                // becoming due or jobs added by other distributed workers.
                // when not activated (non-distributed with no jobs found yet), block indefinitely to avoid pointless DB polling.
                // TriggerJob() sets _activated=true and releases the semaphore when the first job is queued.
                if (await _sem.WaitAsync(_activated ? _semWaitLimit : Timeout.InfiniteTimeSpan, _appCancellation))
                {
                    // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                    // reset/drain _sem CurrentCount to 0 in case multiple releases happened
                    // passing app cancellation here is not needed as it's an immediate return.
                    while (_sem.Wait(0)) { }
                }
            }
            catch (OperationCanceledException) when (_appCancellation.IsCancellationRequested)
            {
                // don't throw. let the main loop exit via its condition check so DrainExecutionsAsync runs
            }
            catch (Exception x)
            {
                _log.CommandParallelExecutionWarning(_commandTypeName, x.Message);
                await Task.Delay(_retryDelay);
            }
        }

        async Task DrainExecutionsAsync()
        {
            await ObserveCompletedExecutions();

            while (executions.Count > 0)
            {
                await Task.WhenAny(executions.Values);
                await ObserveCompletedExecutions();
            }
        }

        async Task ObserveCompletedExecutions()
        {
            if (executions.Count == 0)
                return;

            foreach (var kv in executions.Where(static kv => kv.Value.IsCompleted).ToArray())
            {
                executions.Remove(kv.Key);

                try
                {
                    await kv.Value;
                }
                catch (OperationCanceledException x) when (!_appCancellation.IsCancellationRequested)
                {
                    _log.CommandParallelExecutionWarning(_commandTypeName, x.Message);
                }
                catch (Exception x)
                {
                    _log.CommandParallelExecutionWarning(_commandTypeName, x.Message);
                }
            }
        }

        void CleanupStaleManualCancellationMarkers()
        {
            if (!_nextCleanupOn.HasValue || DateTime.UtcNow < _nextCleanupOn.Value)
                return;

            _nextCleanupOn = null;

            foreach (var kv in _cancellations)
            {
                //only remove if stale and not currently in-flight
                if (kv.Value is null && !(executions.TryGetValue(kv.Key, out var execution) && !execution.IsCompleted))
                    _cancellations.TryRemove(kv.Key, out _);
            }
        }
    }

    CancellationTokenSource? GetOrAddCancellation(Guid trackingId, Func<CancellationTokenSource> factory)
    {
        // ConcurrentDictionary.GetOrAdd may invoke the factory on multiple threads but only store one result.
        // discarded CancellationTokenSource instances (e.g. linked sources with callbacks on long-lived tokens)
        // must be disposed to prevent permanent resource leaks.

        CancellationTokenSource? created = null;

        var cts = _cancellations.GetOrAdd(
            trackingId,
            _ =>
            {
                created = factory();

                return created;
            });

        if (created is not null && !ReferenceEquals(created, cts))
            created.Dispose();

        return cts;
    }
}