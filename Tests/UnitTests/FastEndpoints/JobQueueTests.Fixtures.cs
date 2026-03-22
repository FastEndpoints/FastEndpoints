using System.Linq.Expressions;
using FastEndpoints;
using static QueueTesting.QueueTestSupport;

namespace JobQueue;

public partial class JobQueueTests
{
    sealed class BasicJob : IJobStorageRecord
    {
        public string QueueID { get; set; } = "";
        public Guid TrackingID { get; set; }
        public object Command { get; set; } = null!;
        public DateTime ExecuteAfter { get; set; }
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
    }

    sealed class DistributedJob : IJobStorageRecord
    {
        public string QueueID { get; set; } = "";
        public Guid TrackingID { get; set; }
        public object Command { get; set; } = null!;
        public DateTime ExecuteAfter { get; set; }
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
        public DateTime DequeueAfter { get; set; }
    }

    sealed class RefillTestCommand : ICommand, IHasTrackingID
    {
        public required string Name { get; init; }
        public int Sequence { get; init; }
        public Guid TrackingID { get; set; }
    }

    sealed class RefillTestRecord : IJobStorageRecord
    {
        public string QueueID { get; set; } = "";
        public Guid TrackingID { get; set; }
        public object Command { get; set; } = null!;
        public DateTime ExecuteAfter { get; set; }
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
    }

    sealed class RefillTestStorage : IJobStorageProvider<RefillTestRecord>
    {
        readonly Lock _lock = new();
        readonly Dictionary<Guid, RefillTestRecord> _jobs = [];
        readonly List<int> _requestedLimits = [];
        int _activeExecutions;
        int _maxActiveExecutions;

        public bool DistributedJobProcessingEnabled => false;
        public int MaxActiveExecutions => _maxActiveExecutions;

        public Task StoreJobAsync(RefillTestRecord record, CancellationToken ct)
        {
            lock (_lock)
                _jobs[record.TrackingID] = record;

            return Task.CompletedTask;
        }

        public Task<ICollection<RefillTestRecord>> GetNextBatchAsync(PendingJobSearchParams<RefillTestRecord> parameters)
        {
            var match = parameters.Match.Compile();

            lock (_lock)
            {
                _requestedLimits.Add(parameters.Limit);

                return Task.FromResult<ICollection<RefillTestRecord>>(
                    _jobs.Values
                         .Where(match)
                         .OrderBy(record => ((RefillTestCommand)record.Command).Sequence)
                         .Take(parameters.Limit)
                         .ToArray());
            }
        }

        public Task MarkJobAsCompleteAsync(RefillTestRecord record, CancellationToken ct)
        {
            lock (_lock)
                _jobs[record.TrackingID].IsComplete = true;

            return Task.CompletedTask;
        }

        public Task CancelJobAsync(Guid trackingId, CancellationToken ct)
        {
            lock (_lock)
                _jobs[trackingId].IsComplete = true;

            return Task.CompletedTask;
        }

        public Task OnHandlerExecutionFailureAsync(RefillTestRecord record, Exception exception, CancellationToken ct)
            => Task.CompletedTask;

        public Task PurgeStaleJobsAsync(StaleJobSearchParams<RefillTestRecord> parameters)
            => Task.CompletedTask;

        public int IncrementActiveExecutions()
        {
            var current = Interlocked.Increment(ref _activeExecutions);

            while (true)
            {
                var snapshot = _maxActiveExecutions;

                if (current <= snapshot)
                    break;

                if (Interlocked.CompareExchange(ref _maxActiveExecutions, current, snapshot) == snapshot)
                    break;
            }

            return current;
        }

        public int DecrementActiveExecutions()
            => Interlocked.Decrement(ref _activeExecutions);

        public Task<bool> WaitForCompletionAsync(Guid trackingId, TimeSpan timeout)
            => WaitUntilAsync(
                () =>
                {
                    lock (_lock)
                        return _jobs.TryGetValue(trackingId, out var job) && job.IsComplete;
                },
                timeout);

        public int[] GetRequestedLimitsSnapshot()
        {
            lock (_lock)
                return _requestedLimits.ToArray();
        }
    }

    sealed class RefillTestCommandHandler(RefillTestStorage storage) : ICommandHandler<RefillTestCommand>
    {
        static TaskCompletionSource<bool> _slowCanFinish = NewSignal<bool>();
        static TaskCompletionSource<bool> _fastStarted = NewSignal<bool>();
        static TaskCompletionSource<bool> _thirdStarted = NewSignal<bool>();
        static TaskCompletionSource<bool> _drainStarted = NewSignal<bool>();
        static TaskCompletionSource<bool> _drainCanFinish = NewSignal<bool>();

        public static Task FastStarted => _fastStarted.Task;
        public static Task ThirdStarted => _thirdStarted.Task;
        public static Task DrainStarted => _drainStarted.Task;

        public static void Reset()
        {
            _slowCanFinish = NewSignal<bool>();
            _fastStarted = NewSignal<bool>();
            _thirdStarted = NewSignal<bool>();
            _drainStarted = NewSignal<bool>();
            _drainCanFinish = NewSignal<bool>();
        }

        public static void ReleaseSlow()
            => _slowCanFinish.TrySetResult(true);

        public static void ReleaseDrain()
            => _drainCanFinish.TrySetResult(true);

        public async Task ExecuteAsync(RefillTestCommand command, CancellationToken ct)
        {
            storage.IncrementActiveExecutions();

            try
            {
                switch (command.Name)
                {
                    case "slow":
                        await _slowCanFinish.Task.WaitAsync(ct);

                        break;
                    case "fast":
                        _fastStarted.TrySetResult(true);

                        break;
                    case "third":
                        _thirdStarted.TrySetResult(true);

                        break;
                    case "drain":
                        _drainStarted.TrySetResult(true);
                        await _drainCanFinish.Task;

                        break;
                }
            }
            finally
            {
                storage.DecrementActiveExecutions();
            }
        }
    }

    sealed class DistributedRefillCommand : ICommand, IHasTrackingID
    {
        public required string Name { get; init; }
        public int Sequence { get; init; }
        public Guid TrackingID { get; set; }
    }

    sealed class DistributedRefillRecord : IJobStorageRecord
    {
        public string QueueID { get; set; } = "";
        public Guid TrackingID { get; set; }
        public object Command { get; set; } = null!;
        public DateTime ExecuteAfter { get; set; }
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
        public DateTime DequeueAfter { get; set; }
    }

    sealed class DistributedJobStorage : IJobStorageProvider<DistributedJob>
    {
        readonly Lock _lock = new();
        readonly List<DistributedJob> _jobs = [];

        public bool DistributedJobProcessingEnabled => true;

        public Task StoreJobAsync(DistributedJob record, CancellationToken ct)
        {
            lock (_lock)
                _jobs.Add(record);

            return Task.CompletedTask;
        }

        public Task<ICollection<DistributedJob>> GetNextBatchAsync(PendingJobSearchParams<DistributedJob> parameters)
        {
            var match = parameters.Match.Compile();
            var now = DateTime.UtcNow;
            var leaseTime = parameters.ExecutionTimeLimit == Timeout.InfiniteTimeSpan
                                ? TimeSpan.FromMinutes(5)
                                : parameters.ExecutionTimeLimit;

            lock (_lock)
            {
                var results = _jobs
                              .Where(match)
                              .OrderBy(job => job.TrackingID)
                              .Take(parameters.Limit)
                              .ToArray();

                foreach (var job in results)
                    job.DequeueAfter = now + leaseTime;

                return Task.FromResult<ICollection<DistributedJob>>(results);
            }
        }

        public Task MarkJobAsCompleteAsync(DistributedJob record, CancellationToken ct)
        {
            lock (_lock)
                record.IsComplete = true;

            return Task.CompletedTask;
        }

        public Task CancelJobAsync(Guid trackingId, CancellationToken ct)
        {
            lock (_lock)
                _jobs.Single(job => job.TrackingID == trackingId).IsComplete = true;

            return Task.CompletedTask;
        }

        public Task OnHandlerExecutionFailureAsync(DistributedJob record, Exception exception, CancellationToken ct)
        {
            lock (_lock)
                record.DequeueAfter = DateTime.MinValue;

            return Task.CompletedTask;
        }

        public Task PurgeStaleJobsAsync(StaleJobSearchParams<DistributedJob> parameters)
            => Task.CompletedTask;
    }

    sealed class DistributedRefillStorage : IJobStorageProvider<DistributedRefillRecord>
    {
        readonly Lock _lock = new();
        readonly Dictionary<Guid, DistributedRefillRecord> _jobs = [];
        readonly List<int> _requestedLimits = [];

        public bool DistributedJobProcessingEnabled => true;

        public Task StoreJobAsync(DistributedRefillRecord record, CancellationToken ct)
        {
            lock (_lock)
                _jobs[record.TrackingID] = record;

            return Task.CompletedTask;
        }

        public Task<ICollection<DistributedRefillRecord>> GetNextBatchAsync(PendingJobSearchParams<DistributedRefillRecord> parameters)
        {
            var match = parameters.Match.Compile();
            var now = DateTime.UtcNow;
            var leaseTime = parameters.ExecutionTimeLimit == Timeout.InfiniteTimeSpan
                                ? TimeSpan.FromMinutes(5)
                                : parameters.ExecutionTimeLimit;

            lock (_lock)
            {
                _requestedLimits.Add(parameters.Limit);

                var results = _jobs.Values
                                   .Where(match)
                                   .OrderBy(record => ((DistributedRefillCommand)record.Command).Sequence)
                                   .Take(parameters.Limit)
                                   .ToArray();

                foreach (var job in results)
                    job.DequeueAfter = now + leaseTime;

                return Task.FromResult<ICollection<DistributedRefillRecord>>(results);
            }
        }

        public Task MarkJobAsCompleteAsync(DistributedRefillRecord record, CancellationToken ct)
        {
            lock (_lock)
                _jobs[record.TrackingID].IsComplete = true;

            return Task.CompletedTask;
        }

        public Task CancelJobAsync(Guid trackingId, CancellationToken ct)
        {
            lock (_lock)
                _jobs[trackingId].IsComplete = true;

            return Task.CompletedTask;
        }

        public Task OnHandlerExecutionFailureAsync(DistributedRefillRecord record, Exception exception, CancellationToken ct)
        {
            lock (_lock)
                _jobs[record.TrackingID].DequeueAfter = DateTime.MinValue;

            return Task.CompletedTask;
        }

        public Task PurgeStaleJobsAsync(StaleJobSearchParams<DistributedRefillRecord> parameters)
            => Task.CompletedTask;

        public DateTime GetDequeueAfter(Guid trackingId)
        {
            lock (_lock)
                return _jobs[trackingId].DequeueAfter;
        }

        public Task<bool> WaitForCompletionAsync(Guid trackingId, TimeSpan timeout)
            => WaitUntilAsync(
                () =>
                {
                    lock (_lock)
                        return _jobs.TryGetValue(trackingId, out var job) && job.IsComplete;
                },
                timeout);

        public int[] GetRequestedLimitsSnapshot()
        {
            lock (_lock)
                return _requestedLimits.ToArray();
        }
    }

    sealed class DistributedRefillCommandHandler : ICommandHandler<DistributedRefillCommand>
    {
        static TaskCompletionSource<bool> _slowCanFinish = NewSignal<bool>();
        static TaskCompletionSource<bool> _thirdCanFinish = NewSignal<bool>();
        static TaskCompletionSource<bool> _fastStarted = NewSignal<bool>();
        static TaskCompletionSource<bool> _thirdStarted = NewSignal<bool>();
        static TaskCompletionSource<bool> _fourthStarted = NewSignal<bool>();

        public static Task FastStarted => _fastStarted.Task;
        public static Task ThirdStarted => _thirdStarted.Task;
        public static Task FourthStarted => _fourthStarted.Task;

        public static void Reset()
        {
            _slowCanFinish = NewSignal<bool>();
            _thirdCanFinish = NewSignal<bool>();
            _fastStarted = NewSignal<bool>();
            _thirdStarted = NewSignal<bool>();
            _fourthStarted = NewSignal<bool>();
        }

        public static void ReleaseSlow()
            => _slowCanFinish.TrySetResult(true);

        public static void ReleaseThird()
            => _thirdCanFinish.TrySetResult(true);

        public async Task ExecuteAsync(DistributedRefillCommand command, CancellationToken ct)
        {
            switch (command.Name)
            {
                case "slow":
                    await _slowCanFinish.Task.WaitAsync(ct);

                    break;
                case "fast":
                    _fastStarted.TrySetResult(true);

                    break;
                case "third":
                    _thirdStarted.TrySetResult(true);
                    await _thirdCanFinish.Task.WaitAsync(ct);

                    break;
                case "fourth":
                    _fourthStarted.TrySetResult(true);

                    break;
            }
        }
    }

    sealed class ManualCancelTestCommand : ICommand, IHasTrackingID
    {
        public Guid TrackingID { get; set; }
    }

    sealed class ManualCancelTestCommandHandler : ICommandHandler<ManualCancelTestCommand>
    {
        static TaskCompletionSource<bool> _started = NewSignal<bool>();
        static TaskCompletionSource<bool> _cancellationObserved = NewSignal<bool>();
        static TaskCompletionSource<bool> _resumeAfterCancellation = NewSignal<bool>();
        static TaskCompletionSource<bool> _finished = NewSignal<bool>();

        public static Task Started => _started.Task;
        public static Task CancellationObserved => _cancellationObserved.Task;
        public static Task Finished => _finished.Task;

        public static void Reset()
        {
            _started = NewSignal<bool>();
            _cancellationObserved = NewSignal<bool>();
            _resumeAfterCancellation = NewSignal<bool>();
            _finished = NewSignal<bool>();
        }

        public static void ReleaseAfterCancellation()
            => _resumeAfterCancellation.TrySetResult(true);

        public async Task ExecuteAsync(ManualCancelTestCommand command, CancellationToken ct)
        {
            _started.TrySetResult(true);

            using var reg = ct.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), _cancellationObserved);

            try
            {
                await _cancellationObserved.Task;
                await _resumeAfterCancellation.Task;
                ct.ThrowIfCancellationRequested();
            }
            finally
            {
                _finished.TrySetResult(true);
            }
        }
    }

    sealed class ManualCancelTestRecord : IJobStorageRecord
    {
        public string QueueID { get; set; } = "";
        public Guid TrackingID { get; set; }
        public object Command { get; set; } = null!;
        public DateTime ExecuteAfter { get; set; }
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
    }

    sealed class CancelRaceTestCommand : ICommand, IHasTrackingID
    {
        public Guid TrackingID { get; set; }
    }

    sealed class CancelRaceTestCommandHandler : ICommandHandler<CancelRaceTestCommand>
    {
        static int _executionCount;
        static TaskCompletionSource<bool> _started = NewSignal<bool>();
        static TaskCompletionSource<bool> _firstExecutionFinished = NewSignal<bool>();
        static TaskCompletionSource<bool> _secondExecutionStarted = NewSignal<bool>();

        public static Task Started => _started.Task;
        public static Task FirstExecutionFinished => _firstExecutionFinished.Task;
        public static Task SecondExecutionStarted => _secondExecutionStarted.Task;
        public static int ExecutionCount => Volatile.Read(ref _executionCount);

        public static void Reset()
        {
            Interlocked.Exchange(ref _executionCount, 0);
            _started = NewSignal<bool>();
            _firstExecutionFinished = NewSignal<bool>();
            _secondExecutionStarted = NewSignal<bool>();
        }

        public async Task ExecuteAsync(CancelRaceTestCommand command, CancellationToken ct)
        {
            var executionNumber = Interlocked.Increment(ref _executionCount);

            if (executionNumber == 1)
            {
                _started.TrySetResult(true);

                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                }
                finally
                {
                    _firstExecutionFinished.TrySetResult(true);
                }

                return;
            }

            _secondExecutionStarted.TrySetResult(true);
        }
    }

    sealed class CancelRaceTestRecord : IJobStorageRecord
    {
        public string QueueID { get; set; } = "";
        public Guid TrackingID { get; set; }
        public object Command { get; set; } = null!;
        public DateTime ExecuteAfter { get; set; }
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
    }

    sealed class CancelRaceTestStorage : IJobStorageProvider<CancelRaceTestRecord>
    {
        readonly Lock _lock = new();
        readonly Dictionary<Guid, CancelRaceTestRecord> _jobs = [];
        readonly TaskCompletionSource<bool> _cancelStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource<bool> _allowCancelCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _cancelCount;

        public bool DistributedJobProcessingEnabled => false;
        public int CancelCount => Volatile.Read(ref _cancelCount);
        public Task CancelStarted => _cancelStarted.Task;

        public void ReleaseCancel()
            => _allowCancelCompletion.TrySetResult(true);

        public Task StoreJobAsync(CancelRaceTestRecord record, CancellationToken ct)
        {
            lock (_lock)
                _jobs[record.TrackingID] = record;

            return Task.CompletedTask;
        }

        public Task<ICollection<CancelRaceTestRecord>> GetNextBatchAsync(PendingJobSearchParams<CancelRaceTestRecord> parameters)
        {
            var match = parameters.Match.Compile();

            lock (_lock)
            {
                return Task.FromResult<ICollection<CancelRaceTestRecord>>(
                    _jobs.Values
                         .Where(match)
                         .Take(parameters.Limit)
                         .ToArray());
            }
        }

        public Task MarkJobAsCompleteAsync(CancelRaceTestRecord record, CancellationToken ct)
        {
            lock (_lock)
                _jobs[record.TrackingID].IsComplete = true;

            return Task.CompletedTask;
        }

        public async Task CancelJobAsync(Guid trackingId, CancellationToken ct)
        {
            Interlocked.Increment(ref _cancelCount);
            _cancelStarted.TrySetResult(true);
            await _allowCancelCompletion.Task.WaitAsync(ct);

            lock (_lock)
                _jobs[trackingId].IsComplete = true;
        }

        public Task OnHandlerExecutionFailureAsync(CancelRaceTestRecord record, Exception exception, CancellationToken ct)
            => Task.CompletedTask;

        public Task PurgeStaleJobsAsync(StaleJobSearchParams<CancelRaceTestRecord> parameters)
            => Task.CompletedTask;

        public Task<bool> WaitForCompletionAsync(Guid trackingId, TimeSpan timeout)
            => WaitUntilAsync(
                () =>
                {
                    lock (_lock)
                        return _jobs.TryGetValue(trackingId, out var job) && job.IsComplete;
                },
                timeout);
    }

    sealed class BatchFailureTestCommand : ICommand { }

    sealed class BatchFailureTestRecord : IJobStorageRecord
    {
        public string QueueID { get; set; } = "";
        public Guid TrackingID { get; set; }
        public object Command { get; set; } = null!;
        public DateTime ExecuteAfter { get; set; }
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
    }

    sealed class BatchFailureTestStorage : IJobStorageProvider<BatchFailureTestRecord>
    {
        int _batchAttempts;

        public bool DistributedJobProcessingEnabled => false;
        public int BatchAttempts => Volatile.Read(ref _batchAttempts);

        public Task StoreJobAsync(BatchFailureTestRecord record, CancellationToken ct)
            => Task.CompletedTask;

        public Task<ICollection<BatchFailureTestRecord>> GetNextBatchAsync(PendingJobSearchParams<BatchFailureTestRecord> parameters)
        {
            Interlocked.Increment(ref _batchAttempts);

            return Task.FromException<ICollection<BatchFailureTestRecord>>(new InvalidOperationException("simulated batch failure"));
        }

        public Task MarkJobAsCompleteAsync(BatchFailureTestRecord record, CancellationToken ct)
            => Task.CompletedTask;

        public Task CancelJobAsync(Guid trackingId, CancellationToken ct)
            => Task.CompletedTask;

        public Task OnHandlerExecutionFailureAsync(BatchFailureTestRecord record, Exception exception, CancellationToken ct)
            => Task.CompletedTask;

        public Task PurgeStaleJobsAsync(StaleJobSearchParams<BatchFailureTestRecord> parameters)
            => Task.CompletedTask;
    }

    sealed class ManualCancelTestStorage(int cancelFailures = 0) : IJobStorageProvider<ManualCancelTestRecord>
    {
        readonly Lock _lock = new();
        readonly Dictionary<Guid, ManualCancelTestRecord> _jobs = [];
        int _remainingCancelFailures = cancelFailures;
        int _failureCount;
        int _cancelCount;
        int _cancelFailureCount;

        public bool DistributedJobProcessingEnabled => false;
        public int FailureCount => _failureCount;
        public int CancelCount => _cancelCount;
        public int CancelFailureCount => _cancelFailureCount;

        public Task StoreJobAsync(ManualCancelTestRecord record, CancellationToken ct)
        {
            lock (_lock)
                _jobs[record.TrackingID] = record;

            return Task.CompletedTask;
        }

        public Task<ICollection<ManualCancelTestRecord>> GetNextBatchAsync(PendingJobSearchParams<ManualCancelTestRecord> parameters)
        {
            var match = parameters.Match.Compile();

            lock (_lock)
            {
                return Task.FromResult<ICollection<ManualCancelTestRecord>>(
                    _jobs.Values
                         .Where(match)
                         .Take(parameters.Limit)
                         .ToArray());
            }
        }

        public Task MarkJobAsCompleteAsync(ManualCancelTestRecord record, CancellationToken ct)
        {
            lock (_lock)
                _jobs[record.TrackingID].IsComplete = true;

            return Task.CompletedTask;
        }

        public Task CancelJobAsync(Guid trackingId, CancellationToken ct)
        {
            Interlocked.Increment(ref _cancelCount);

            lock (_lock)
            {
                if (_remainingCancelFailures > 0)
                {
                    _remainingCancelFailures--;
                    Interlocked.Increment(ref _cancelFailureCount);

                    return Task.FromException(new InvalidOperationException("simulated cancel failure"));
                }

                _jobs[trackingId].IsComplete = true;
            }

            return Task.CompletedTask;
        }

        public Task OnHandlerExecutionFailureAsync(ManualCancelTestRecord record, Exception exception, CancellationToken ct)
        {
            Interlocked.Increment(ref _failureCount);

            return Task.CompletedTask;
        }

        public Task PurgeStaleJobsAsync(StaleJobSearchParams<ManualCancelTestRecord> parameters)
            => Task.CompletedTask;

        public Task<bool> WaitForCompletionAsync(Guid trackingId, TimeSpan timeout)
            => WaitUntilAsync(
                () =>
                {
                    lock (_lock)
                        return _jobs.TryGetValue(trackingId, out var job) && job.IsComplete;
                },
                timeout);
    }

    sealed class PreCancelledExecutionRaceTestStorage : IJobStorageProvider<ManualCancelTestRecord>
    {
        readonly Lock _lock = new();
        readonly Dictionary<Guid, ManualCancelTestRecord> _jobs = [];
        readonly TaskCompletionSource<bool> _batchFetchStarted = NewSignal<bool>();
        readonly TaskCompletionSource<bool> _allowBatchFetch = NewSignal<bool>();
        readonly TaskCompletionSource<bool> _batchReturned = NewSignal<bool>();
        readonly TaskCompletionSource<bool> _cancelStarted = NewSignal<bool>();
        readonly TaskCompletionSource<bool> _allowCancelCompletion = NewSignal<bool>();
        int _batchReturnCount;
        int _cancelCount;

        public bool DistributedJobProcessingEnabled => false;
        public Task BatchFetchStarted => _batchFetchStarted.Task;
        public Task BatchReturned => _batchReturned.Task;
        public Task CancelStarted => _cancelStarted.Task;
        public int BatchReturnCount => Volatile.Read(ref _batchReturnCount);
        public int CancelCount => Volatile.Read(ref _cancelCount);

        public void ReleaseBatchFetch()
            => _allowBatchFetch.TrySetResult(true);

        public void ReleaseCancel()
            => _allowCancelCompletion.TrySetResult(true);

        public Task StoreJobAsync(ManualCancelTestRecord record, CancellationToken ct)
        {
            lock (_lock)
                _jobs[record.TrackingID] = record;

            return Task.CompletedTask;
        }

        public async Task<ICollection<ManualCancelTestRecord>> GetNextBatchAsync(PendingJobSearchParams<ManualCancelTestRecord> parameters)
        {
            _batchFetchStarted.TrySetResult(true);
            await _allowBatchFetch.Task.WaitAsync(parameters.CancellationToken);

            var match = parameters.Match.Compile();
            ManualCancelTestRecord[] jobs;

            lock (_lock)
            {
                jobs = _jobs.Values
                            .Where(match)
                            .Take(parameters.Limit)
                            .ToArray();
            }

            Interlocked.Increment(ref _batchReturnCount);
            _batchReturned.TrySetResult(true);

            return jobs;
        }

        public Task MarkJobAsCompleteAsync(ManualCancelTestRecord record, CancellationToken ct)
        {
            lock (_lock)
                _jobs[record.TrackingID].IsComplete = true;

            return Task.CompletedTask;
        }

        public async Task CancelJobAsync(Guid trackingId, CancellationToken ct)
        {
            Interlocked.Increment(ref _cancelCount);
            _cancelStarted.TrySetResult(true);
            await _allowCancelCompletion.Task.WaitAsync(ct);

            lock (_lock)
                _jobs[trackingId].IsComplete = true;
        }

        public Task OnHandlerExecutionFailureAsync(ManualCancelTestRecord record, Exception exception, CancellationToken ct)
            => Task.CompletedTask;

        public Task PurgeStaleJobsAsync(StaleJobSearchParams<ManualCancelTestRecord> parameters)
            => Task.CompletedTask;

        public Task<bool> WaitForCompletionAsync(Guid trackingId, TimeSpan timeout)
            => WaitUntilAsync(
                () =>
                {
                    lock (_lock)
                        return _jobs.TryGetValue(trackingId, out var job) && job.IsComplete;
                },
                timeout);
    }

    sealed class ResultIgnoringTestCommand : ICommand<string>, IHasTrackingID
    {
        public Guid TrackingID { get; set; }
        public string Payload { get; set; } = "";
    }

    sealed class ResultIgnoringTestRecord : IJobStorageRecord
    {
        public string QueueID { get; set; } = "";
        public Guid TrackingID { get; set; }
        public object Command { get; set; } = null!;
        public DateTime ExecuteAfter { get; set; }
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
    }

    sealed class ResultIgnoringTestStorage : IJobStorageProvider<ResultIgnoringTestRecord>
    {
        readonly Lock _lock = new();
        readonly Dictionary<Guid, ResultIgnoringTestRecord> _jobs = [];

        public bool DistributedJobProcessingEnabled => false;

        public Task StoreJobAsync(ResultIgnoringTestRecord record, CancellationToken ct)
        {
            lock (_lock)
                _jobs[record.TrackingID] = record;

            return Task.CompletedTask;
        }

        public Task<ICollection<ResultIgnoringTestRecord>> GetNextBatchAsync(PendingJobSearchParams<ResultIgnoringTestRecord> parameters)
        {
            var match = parameters.Match.Compile();

            lock (_lock)
            {
                return Task.FromResult<ICollection<ResultIgnoringTestRecord>>(
                    _jobs.Values
                         .Where(match)
                         .Take(parameters.Limit)
                         .ToArray());
            }
        }

        public Task MarkJobAsCompleteAsync(ResultIgnoringTestRecord record, CancellationToken ct)
        {
            lock (_lock)
                _jobs[record.TrackingID].IsComplete = true;

            return Task.CompletedTask;
        }

        public Task CancelJobAsync(Guid trackingId, CancellationToken ct)
        {
            lock (_lock)
                _jobs[trackingId].IsComplete = true;

            return Task.CompletedTask;
        }

        public Task OnHandlerExecutionFailureAsync(ResultIgnoringTestRecord record, Exception exception, CancellationToken ct)
            => Task.CompletedTask;

        public Task PurgeStaleJobsAsync(StaleJobSearchParams<ResultIgnoringTestRecord> parameters)
            => Task.CompletedTask;

        public Task<bool> WaitForCompletionAsync(Guid trackingId, TimeSpan timeout)
            => WaitUntilAsync(
                () =>
                {
                    lock (_lock)
                        return _jobs.TryGetValue(trackingId, out var job) && job.IsComplete;
                },
                timeout);

        public ResultIgnoringTestRecord? GetJob(Guid trackingId)
        {
            lock (_lock)
                return _jobs.GetValueOrDefault(trackingId);
        }
    }

    sealed class ResultIgnoringTestCommandHandler : ICommandHandler<ResultIgnoringTestCommand, string>
    {
        public Task<string> ExecuteAsync(ResultIgnoringTestCommand command, CancellationToken ct)
            => Task.FromResult($"handled:{command.Payload}");
    }

    sealed class ResultCapableVoidTestCommand : ICommand, IHasTrackingID
    {
        public Guid TrackingID { get; set; }
    }

    sealed class ResultCapableVoidTestRecord : IJobStorageRecord
    {
        public string QueueID { get; set; } = "";
        public Guid TrackingID { get; set; }
        public object Command { get; set; } = null!;
        public DateTime ExecuteAfter { get; set; }
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
    }

    sealed class ResultCapableVoidTestStorage : IJobStorageProvider<ResultCapableVoidTestRecord>, IJobResultProvider
    {
        readonly Lock _lock = new();
        readonly Dictionary<Guid, ResultCapableVoidTestRecord> _jobs = [];
        int _storeResultCalls;

        public bool DistributedJobProcessingEnabled => false;
        public int StoreResultCalls => _storeResultCalls;

        public Task StoreJobAsync(ResultCapableVoidTestRecord record, CancellationToken ct)
        {
            lock (_lock)
                _jobs[record.TrackingID] = record;

            return Task.CompletedTask;
        }

        public Task<ICollection<ResultCapableVoidTestRecord>> GetNextBatchAsync(PendingJobSearchParams<ResultCapableVoidTestRecord> parameters)
        {
            var match = parameters.Match.Compile();

            lock (_lock)
            {
                return Task.FromResult<ICollection<ResultCapableVoidTestRecord>>(
                    _jobs.Values
                         .Where(match)
                         .Take(parameters.Limit)
                         .ToArray());
            }
        }

        public Task MarkJobAsCompleteAsync(ResultCapableVoidTestRecord record, CancellationToken ct)
        {
            lock (_lock)
                _jobs[record.TrackingID].IsComplete = true;

            return Task.CompletedTask;
        }

        public Task CancelJobAsync(Guid trackingId, CancellationToken ct)
        {
            lock (_lock)
                _jobs[trackingId].IsComplete = true;

            return Task.CompletedTask;
        }

        public Task OnHandlerExecutionFailureAsync(ResultCapableVoidTestRecord record, Exception exception, CancellationToken ct)
            => Task.CompletedTask;

        public Task PurgeStaleJobsAsync(StaleJobSearchParams<ResultCapableVoidTestRecord> parameters)
            => Task.CompletedTask;

        public Task StoreJobResultAsync<TResult>(Guid trackingId, TResult result, CancellationToken ct)
        {
            Interlocked.Increment(ref _storeResultCalls);

            throw new InvalidOperationException("void commands must not persist results");
        }

        public Task<TResult?> GetJobResultAsync<TResult>(Guid trackingId, CancellationToken ct)
            => Task.FromResult<TResult?>(default);

        public Task<bool> WaitForCompletionAsync(Guid trackingId, TimeSpan timeout)
            => WaitUntilAsync(
                () =>
                {
                    lock (_lock)
                        return _jobs.TryGetValue(trackingId, out var job) && job.IsComplete;
                },
                timeout);
    }

    sealed class ResultCapableVoidTestCommandHandler : ICommandHandler<ResultCapableVoidTestCommand>
    {
        public Task ExecuteAsync(ResultCapableVoidTestCommand command, CancellationToken ct)
            => Task.CompletedTask;
    }

    sealed class PersistenceRetryTestCommand : ICommand<string>, IHasTrackingID
    {
        public Guid TrackingID { get; set; }
        public TimeSpan ExecutionDelay { get; init; }
        public bool WaitForCancellation { get; init; }
        public bool ShouldThrow { get; init; }
        public string FailureMessage { get; init; } = "simulated execution failure";
        public string ResultText { get; init; } = "ok";
    }

    sealed class PersistenceRetryTestRecord : IJobStorageRecord, IJobResultStorage
    {
        public string QueueID { get; set; } = "";
        public Guid TrackingID { get; set; }
        public object Command { get; set; } = null!;
        public bool ReturnNullCommandOnGet { get; set; }
        public DateTime ExecuteAfter { get; set; }
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
        public object? Result { get; set; }

        public TCommand GetCommand<TCommand>() where TCommand : class, ICommandBase
            => ReturnNullCommandOnGet ? null! : (TCommand)Command;
    }

    sealed class PersistenceRetryTestStorage(int storeResultFailures = 0,
                                            int markCompleteFailures = 0,
                                            int executionFailurePersistFailures = 0,
                                            int getResultFailures = 0)
        : IJobStorageProvider<PersistenceRetryTestRecord>, IJobResultProvider
    {
        readonly Lock _lock = new();
        readonly Dictionary<Guid, PersistenceRetryTestRecord> _jobs = [];
        readonly Dictionary<Guid, bool> _recordedFailureCancellationStates = [];
        int _remainingStoreResultFailures = storeResultFailures;
        int _remainingMarkCompleteFailures = markCompleteFailures;
        int _remainingExecutionFailurePersistFailures = executionFailurePersistFailures;
        int _remainingGetResultFailures = getResultFailures;
        int _executionCount;
        int _executionFailureAttempts;
        int _storeResultAttempts;
        int _markCompleteAttempts;
        int _getResultAttempts;
        int _recordedFailureCount;

        public bool DistributedJobProcessingEnabled => false;
        public int ExecutionCount => Volatile.Read(ref _executionCount);
        public int ExecutionFailureAttempts => Volatile.Read(ref _executionFailureAttempts);
        public int RecordedFailureCount => Volatile.Read(ref _recordedFailureCount);
        public int StoreResultAttempts => Volatile.Read(ref _storeResultAttempts);
        public int MarkCompleteAttempts => Volatile.Read(ref _markCompleteAttempts);
        public int GetResultAttempts => Volatile.Read(ref _getResultAttempts);

        public Task StoreJobAsync(PersistenceRetryTestRecord record, CancellationToken ct)
        {
            lock (_lock)
                _jobs[record.TrackingID] = Clone(record);

            return Task.CompletedTask;
        }

        public Task<ICollection<PersistenceRetryTestRecord>> GetNextBatchAsync(PendingJobSearchParams<PersistenceRetryTestRecord> parameters)
        {
            var match = parameters.Match.Compile();

            lock (_lock)
            {
                return Task.FromResult<ICollection<PersistenceRetryTestRecord>>(
                    _jobs.Values
                         .Where(match)
                         .Select(Clone)
                         .Take(parameters.Limit)
                         .ToArray());
            }
        }

        public Task MarkJobAsCompleteAsync(PersistenceRetryTestRecord record, CancellationToken ct)
        {
            Interlocked.Increment(ref _markCompleteAttempts);

            lock (_lock)
            {
                if (_remainingMarkCompleteFailures > 0)
                {
                    _remainingMarkCompleteFailures--;

                    return Task.FromException(new InvalidOperationException("simulated completion failure"));
                }

                _jobs[record.TrackingID].IsComplete = true;
            }

            return Task.CompletedTask;
        }

        public Task CancelJobAsync(Guid trackingId, CancellationToken ct)
        {
            lock (_lock)
                _jobs[trackingId].IsComplete = true;

            return Task.CompletedTask;
        }

        public Task OnHandlerExecutionFailureAsync(PersistenceRetryTestRecord record, Exception exception, CancellationToken ct)
        {
            Interlocked.Increment(ref _executionFailureAttempts);

            lock (_lock)
            {
                if (_remainingExecutionFailurePersistFailures > 0)
                {
                    _remainingExecutionFailurePersistFailures--;

                    return Task.FromException(new InvalidOperationException("simulated execution failure persistence error"));
                }

                _jobs[record.TrackingID].IsComplete = true;
                _recordedFailureCancellationStates[record.TrackingID] = exception is OperationCanceledException;
                Interlocked.Increment(ref _recordedFailureCount);
            }

            return Task.CompletedTask;
        }

        public Task PurgeStaleJobsAsync(StaleJobSearchParams<PersistenceRetryTestRecord> parameters)
            => Task.CompletedTask;

        public Task StoreJobResultAsync<TResult>(Guid trackingId, TResult result, CancellationToken ct)
        {
            Interlocked.Increment(ref _storeResultAttempts);

            lock (_lock)
            {
                if (_remainingStoreResultFailures > 0)
                {
                    _remainingStoreResultFailures--;

                    return Task.FromException(new InvalidOperationException("simulated result storage failure"));
                }

                ((IJobResultStorage)_jobs[trackingId]).SetResult(result);
            }

            return Task.CompletedTask;
        }

        public Task<TResult?> GetJobResultAsync<TResult>(Guid trackingId, CancellationToken ct)
        {
            Interlocked.Increment(ref _getResultAttempts);

            lock (_lock)
            {
                if (_remainingGetResultFailures > 0)
                {
                    _remainingGetResultFailures--;

                    return Task.FromException<TResult?>(new InvalidOperationException("simulated result retrieval failure"));
                }

                return Task.FromResult(_jobs.TryGetValue(trackingId, out var job) ? ((IJobResultStorage)job).GetResult<TResult>() : default);
            }
        }

        public void IncrementExecutionCount()
            => Interlocked.Increment(ref _executionCount);

        public Task<bool> WaitForCompletionAsync(Guid trackingId, TimeSpan timeout)
            => WaitUntilAsync(
                () =>
                {
                    lock (_lock)
                        return _jobs.TryGetValue(trackingId, out var job) && job.IsComplete;
                },
                timeout);

        public Task<bool> WaitForFailureRecordedAsync(Guid trackingId, TimeSpan timeout)
            => WaitUntilAsync(
                () =>
                {
                    lock (_lock)
                        return _recordedFailureCancellationStates.ContainsKey(trackingId);
                },
                timeout);

        public bool WasRecordedFailureCancellation(Guid trackingId)
        {
            lock (_lock)
                return _recordedFailureCancellationStates[trackingId];
        }

        public Task<bool> WaitForResultAsync(Guid trackingId, string expectedResult, TimeSpan timeout)
            => WaitUntilAsync(
                () =>
                {
                    lock (_lock)
                        return _jobs.TryGetValue(trackingId, out var job) && Equals(job.Result, expectedResult);
                },
                timeout);

        static PersistenceRetryTestRecord Clone(PersistenceRetryTestRecord record)
            => new()
            {
                QueueID = record.QueueID,
                TrackingID = record.TrackingID,
                Command = record.Command,
                ReturnNullCommandOnGet = record.ReturnNullCommandOnGet,
                ExecuteAfter = record.ExecuteAfter,
                ExpireOn = record.ExpireOn,
                IsComplete = record.IsComplete,
                Result = record.Result
            };
    }

    sealed class PersistenceRetryTestCommandHandler(PersistenceRetryTestStorage storage) : ICommandHandler<PersistenceRetryTestCommand, string>
    {
        public async Task<string> ExecuteAsync(PersistenceRetryTestCommand command, CancellationToken ct)
        {
            storage.IncrementExecutionCount();

            if (command.WaitForCancellation)
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);

            if (command.ExecutionDelay > TimeSpan.Zero)
                await Task.Delay(command.ExecutionDelay, ct);

            if (command.ShouldThrow)
                throw new InvalidOperationException(command.FailureMessage);

            return command.ResultText;
        }
    }

    static DistributedJob CreateDistributedJob(DateTime now,
                                               string queueId = "test-queue",
                                               string command = "cmd",
                                               DateTime? executeAfter = null,
                                               DateTime? expireOn = null,
                                               DateTime? dequeueAfter = null)
        => new()
        {
            QueueID = queueId,
            TrackingID = Guid.NewGuid(),
            Command = command,
            ExecuteAfter = executeAfter ?? now.AddMinutes(-1),
            ExpireOn = expireOn ?? now.AddHours(1),
            DequeueAfter = dequeueAfter ?? DateTime.MinValue
        };

    static PendingJobSearchParams<DistributedJob> CreateDistributedSearchParams(DateTime now, string queueId = "test-queue", int limit = 1)
        => new()
        {
            QueueID = queueId,
            Match = PendingDistributedJobs(queueId, now),
            Limit = limit,
            ExecutionTimeLimit = TimeSpan.FromMinutes(10)
        };

    static Expression<Func<DistributedJob, bool>> PendingDistributedJobs(string queueId, DateTime now)
        => job => job.QueueID == queueId &&
                  !job.IsComplete &&
                  job.ExecuteAfter <= now &&
                  job.ExpireOn >= now &&
                  job.DequeueAfter <= now;

    static async Task StoreJobsAsync(DistributedJobStorage storage, params DistributedJob[] jobs)
    {
        foreach (var job in jobs)
            await storage.StoreJobAsync(job, CancellationToken.None);
    }
}
