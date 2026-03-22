using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using FastEndpoints.JobsQueues;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

// instantiated by DI as singleton
[SuppressMessage("Reliability", "CA2016:Forward the \'CancellationToken\' parameter to methods"),
 SuppressMessage("ReSharper", "MethodSupportsCancellation"),
 SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
sealed class JobQueue<TCommand, TResult, TStorageRecord, TStorageProvider> : JobQueueBase
    where TCommand : class, ICommandBase
    where TStorageRecord : class, IJobStorageRecord, new()
    where TStorageProvider : class, IJobStorageProvider<TStorageRecord>
{
    enum QueueState
    {
        /// <summary>
        /// Non-distributed queue that hasn't seen any jobs yet. Blocks indefinitely on the semaphore (no DB polling).
        /// Initial probe fetch skips ExecuteAfter filtering to discover any existing jobs.
        /// </summary>
        Dormant,

        /// <summary>
        /// Queue has discovered or received jobs. Polls the DB periodically with <see cref="_semWaitLimit" /> timeout.
        /// </summary>
        Active
    }

    static readonly Type _tCommand = typeof(TCommand);
    static readonly string _commandTypeName = _tCommand.FullName!;

    internal Task ExecutorTask { get; private set; } = Task.CompletedTask;
    public static readonly string QueueID = _commandTypeName.ToHash(); //public due to: https://github.com/FastEndpoints/FastEndpoints/issues/468

    readonly JobCancellationTracker _cancellation = new();
    readonly CancellationToken _appCancellation;
    readonly TStorageProvider _storage;
    readonly IJobResultProvider? _resultStorage;
    readonly SemaphoreSlim _sem = new(0);
    readonly ILogger _log;
    readonly bool _isDistributed;
    QueueState _state; // transitions from Dormant -> Active (never back)
    int _maxConcurrency = Environment.ProcessorCount;
    TimeSpan _executionTimeLimit;
    TimeSpan _semWaitLimit;
    TimeSpan _retryDelay = TimeSpan.FromSeconds(5);

    public JobQueue(TStorageProvider storageProvider, IHostApplicationLifetime appLife, ILogger<JobQueue<TCommand, TResult, TStorageRecord, TStorageProvider>> logger)
    {
        JobQueues[_tCommand] = this;
        _storage = storageProvider;
        _resultStorage = storageProvider as IJobResultProvider;
        _isDistributed = storageProvider.DistributedJobProcessingEnabled;
        _state = _isDistributed ? QueueState.Active : QueueState.Dormant;
        _appCancellation = appLife.ApplicationStopping;
        _log = logger;
        JobStorage<TStorageRecord, TStorageProvider>.Initialize(_storage, _appCancellation, _log);
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
        _state = QueueState.Active;
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

        return tRes != tResult
                   ? throw new InvalidOperationException($"The correct result type for the command [{cmdName}] should be: [{tResult.FullName}]! You specified: [{tRes.FullName}]!")
                   : s.GetJobResultAsync<TRes>(trackingId, ct);
    }

    protected override Task StoreJobResultAsync<TRes>(Guid trackingId, TRes result, CancellationToken ct) where TRes : default
    {
        if (_storage is not IJobResultProvider s)
            throw new NotSupportedException($"Please implement the  interface '{nameof(IJobResultProvider)}' on the job storage provider!");

        var tRes = typeof(TRes);
        var tResult = typeof(TResult);
        var cmdName = typeof(TCommand).FullName;

        return tRes != tResult
                   ? throw new InvalidOperationException($"The correct result type for the command [{cmdName}] should be: [{tResult.FullName}]! You specified: [{tRes.FullName}]!")
                   : s.StoreJobResultAsync(trackingId, result, ct);
    }

    protected override async Task CancelJobAsync(Guid trackingId, CancellationToken ct)
    {
        // if job is executing, fetch the null marker added by ExecuteCommand, or the pre-canceled cts
        var cts = _cancellation.GetCancellationOrMarker(trackingId);

        if (cts is null)
        {
            await _storage.CancelJobAsync(trackingId, ct); // retry persisting manual cancellation in case a previous attempt failed or the job is still in flight.

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

        try
        {
            // persist manual cancellation before signaling the token so the executor
            // can't pick the same job again while storage still considers it pending.
            await _storage.CancelJobAsync(trackingId, ct);
        }
        catch
        {
            if (rollbackManualMarkerOnStorageFailure)
                _cancellation.Remove(trackingId);

            throw;
        }

        if (!_cancellation.TryMarkForManualCancel(trackingId, cts))
            return;

        try
        {
            if (!cts.IsCancellationRequested)
                cts.Cancel(false);
        }
        catch (ObjectDisposedException) { }
    }

    async Task CommandExecutorTask()
    {
        var executions = new Dictionary<Guid, Task>();

        while (!_appCancellation.IsCancellationRequested)
        {
            await ObserveCompletedExecutions(executions);

            _cancellation.CleanupStaleMarkers(id => executions.TryGetValue(id, out var exec) && !exec.IsCompleted);

            if (executions.Count < _maxConcurrency)
            {
                var availableSlots = _maxConcurrency - executions.Count;
                ICollection<TStorageRecord> records;

                try
                {
                    // only true in non-distributed mode on the first iteration, making 'ExecuteAfter <= now' in the Match filter ineffective,
                    // causing future jobs to get fetched as well. in distributed mode, all instances must periodically poll.
                    var isDormant = _state == QueueState.Dormant;

                    // capture once so providers that translate predicates can parameterize it cleanly
                    var now = DateTime.UtcNow;

                    Expression<Func<TStorageRecord, bool>> matchExpr = _isDistributed
                                                                           ? r => r.QueueID == QueueID &&
                                                                                  !r.IsComplete &&
                                                                                  (isDormant || r.ExecuteAfter <= now) &&
                                                                                  r.ExpireOn >= now &&
                                                                                  r.DequeueAfter <= now
                                                                           : r => r.QueueID == QueueID &&
                                                                                  !r.IsComplete &&
                                                                                  (isDormant || r.ExecuteAfter <= now) &&
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

                    if (isDormant && records.Count > 0) // eligible + future jobs found during probe
                        _state = QueueState.Active;

                    if (isDormant || executions.Count > 0)
                    {
                        IEnumerable<TStorageRecord> filtered = records;

                        if (isDormant)
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

                    if (await DelayRetryAndCheckShutdown())
                        break;

                    continue;
                }

                if (records.Count > 0)
                {
                    _state = QueueState.Active;

                    foreach (var record in records.DistinctBy(r => r.TrackingID).Take(availableSlots)) //dedupe by TrackingID prevents ghost executions
                        executions[record.TrackingID] = ExecuteCommand(record);

                    if (executions.Count == _maxConcurrency)
                        continue;
                }

                await WaitForSignal();

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

                if (await DelayRetryAndCheckShutdown())
                    break;
            }
        }

        await DrainExecutions(executions);
    }

    async Task ExecuteCommand(TStorageRecord record)
    {
        // don't do 'using' on the result here since it could be the pre-canceled static TCS which shouldn't be disposed.
        var cts = _cancellation.GetOrAdd(
            record.TrackingID,
            () =>
            {
                var s = CancellationTokenSource.CreateLinkedTokenSource(_appCancellation);
                s.CancelAfter(_executionTimeLimit);

                return s;
            });

        try
        {
            // don't execute this job because cancellation has been requested already
            // the cts will be null or already canceled if the entry was created by a call to CancelJobAsync() before the job was picked up for execution
            if (cts is null || cts.IsCancellationRequested)
                return;

            //if cts is not null/canceled, proceed with job execution as cancellation has not been requested yet.
            try
            {
                var cmd = record.GetCommand<TCommand>();

                switch (cmd)
                {
                    case ICommand c:
                        record.Command = cmd; //needed in case user does whole record (non-partial) updates via storage provider.
                        await c.ExecuteAsync(cts.Token);

                        break;
                    case ICommand<TResult> cr:
                        record.Command = cmd; //needed in case user does whole record (non-partial) updates via storage provider.
                        var result = await cr.ExecuteAsync(cts.Token);
                        if (record is IJobResultStorage rec)
                            rec.SetResult(result);

                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Job [{record.TrackingID}] in queue [{QueueID}] could not be deserialized into an executable [{_commandTypeName}] command.");
                }

                _cancellation.Remove(record.TrackingID); // remove entry on completion. cancellations are not possible/valid after this point.
            }
            catch (Exception x) when (x is OperationCanceledException && !_appCancellation.IsCancellationRequested && _cancellation.IsManuallyCancelled(record.TrackingID))
            {
                // don't treat as a handler execution failure when manually canceled mid-execution
                _cancellation.Remove(record.TrackingID);
                _log.JobCancelledManually(_commandTypeName, record.TrackingID);

                return;
            }
            catch (Exception x)
            {
                _cancellation.Remove(record.TrackingID); // remove entry on execution error to allow obtaining a new cts on retry/reentry

                if (x is not OperationCanceledException || !_appCancellation.IsCancellationRequested)
                    _log.CommandExecutionCritical(_commandTypeName, x.Message);

                if (_resultStorage is not null && record is IJobResultStorage failedRec)
                {
                    try
                    {
                        // fetch the last known result in case the job handler stored intermediate results before failing,
                        // so it can be passed to OnHandlerExecutionFailureAsync
                        failedRec.SetResult(await GetJobResultAsync<TResult>(record.TrackingID, CancellationToken.None)); //don't allow canceling.
                    }
                    catch (Exception xx)
                    {
                        _log.StorageGetJobResultError(QueueID, _commandTypeName, xx.Message);
                    }
                }

                // if _appCancellation is used, ORMs could throw OperationCanceledException without actually executing DB operations during app shutdown.
                await RetryUntilSuccessOrShutdownAsync(
                    operation: () => _storage.OnHandlerExecutionFailureAsync(record, x, CancellationToken.None),
                    logError: msg => _log.StorageOnExecutionFailureError(QueueID, _commandTypeName, msg),
                    earlyBreak: () => _cancellation.IsManuallyCancelled(record.TrackingID));

                return; //abort execution here
            }

            if (_resultStorage is not null && record is IJobResultStorage resultRec)
            {
                // if _appCancellation is used here, ORMs could throw without executing the store operation during app shutdown, causing result to get lost.
                await RetryUntilSuccessOrShutdownAsync(
                    operation: () => _resultStorage.StoreJobResultAsync(record.TrackingID, resultRec.GetResult<TResult>(), CancellationToken.None),
                    logError: msg => _log.StorageStoreJobResultError(QueueID, _commandTypeName, msg));
            }

            record.IsComplete = true;

            // if _appCancellation is used, ORMs could throw OperationCanceledException without actually executing DB operations during app shutdown.
            await RetryUntilSuccessOrShutdownAsync(
                operation: () => _storage.MarkJobAsCompleteAsync(record, CancellationToken.None),
                logError: msg => _log.StorageMarkAsCompleteError(QueueID, _commandTypeName, msg));
        }
        finally
        {
            JobCancellationTracker.SafeDispose(cts);
        }
    }

    async Task WaitForSignal()
    {
        try
        {
            // when active (distributed mode, or jobs have been found), wait with a timeout so we periodically re-check
            // for future jobs becoming due or jobs added by other distributed workers.
            // when dormant (non-distributed with no jobs found yet), block indefinitely to avoid pointless DB polling.
            // TriggerJob() transitions to Active and releases the semaphore when the first job is queued.
            if (await _sem.WaitAsync(_state == QueueState.Active ? _semWaitLimit : Timeout.InfiniteTimeSpan, _appCancellation))
            {
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
            await DelayRetryAndCheckShutdown(); //prevent immediate retry.
        }
    }

    async Task DrainExecutions(Dictionary<Guid, Task> executions)
    {
        await ObserveCompletedExecutions(executions);

        while (executions.Count > 0)
        {
            await Task.WhenAny(executions.Values);
            await ObserveCompletedExecutions(executions);
        }
    }

    async Task ObserveCompletedExecutions(Dictionary<Guid, Task> executions)
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

    async Task<bool> DelayRetryAndCheckShutdown()
    {
        try
        {
            await Task.Delay(_retryDelay, _appCancellation);

            return false;
        }
        catch (OperationCanceledException) when (_appCancellation.IsCancellationRequested)
        {
            return true;
        }
    }

    async Task RetryUntilSuccessOrShutdownAsync(Func<Task> operation, Action<string> logError, Func<bool>? earlyBreak = null)
    {
        while (true)
        {
            try
            {
                await operation();

                return;
            }
            catch (Exception x)
            {
                logError(x.Message);

                if (_appCancellation.IsCancellationRequested || earlyBreak?.Invoke() == true)
                    return;

                if (await DelayRetryAndCheckShutdown())
                    return;
            }
        }
    }
}