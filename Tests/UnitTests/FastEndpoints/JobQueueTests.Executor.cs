using FastEndpoints;
using Xunit;

namespace JobQueue;

public partial class JobQueueTests
{
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
        static TaskCompletionSource<bool> _slowCanFinish = NewSignal();
        static TaskCompletionSource<bool> _fastStarted = NewSignal();
        static TaskCompletionSource<bool> _thirdStarted = NewSignal();
        static TaskCompletionSource<bool> _drainStarted = NewSignal();
        static TaskCompletionSource<bool> _drainCanFinish = NewSignal();

        public static Task FastStarted => _fastStarted.Task;
        public static Task ThirdStarted => _thirdStarted.Task;
        public static Task DrainStarted => _drainStarted.Task;

        public static void Reset()
        {
            _slowCanFinish = NewSignal();
            _fastStarted = NewSignal();
            _thirdStarted = NewSignal();
            _drainStarted = NewSignal();
            _drainCanFinish = NewSignal();
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
        static TaskCompletionSource<bool> _slowCanFinish = NewSignal();
        static TaskCompletionSource<bool> _thirdCanFinish = NewSignal();
        static TaskCompletionSource<bool> _fastStarted = NewSignal();
        static TaskCompletionSource<bool> _thirdStarted = NewSignal();
        static TaskCompletionSource<bool> _fourthStarted = NewSignal();

        public static Task FastStarted => _fastStarted.Task;
        public static Task ThirdStarted => _thirdStarted.Task;
        public static Task FourthStarted => _fourthStarted.Task;

        public static void Reset()
        {
            _slowCanFinish = NewSignal();
            _thirdCanFinish = NewSignal();
            _fastStarted = NewSignal();
            _thirdStarted = NewSignal();
            _fourthStarted = NewSignal();
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

    [Fact]
    public async Task executor_refills_a_freed_slot_without_waiting_for_the_whole_batch()
    {
        RefillTestCommandHandler.Reset();
        var storage = new RefillTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateRefillQueue(storage, appStopping);
        queue.SetLimits(2, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20));

        var slow = new RefillTestCommand { Name = "slow", Sequence = 0 };
        var fast = new RefillTestCommand { Name = "fast", Sequence = 1 };
        var third = new RefillTestCommand { Name = "third", Sequence = 2 };

        await slow.QueueJobAsync(ct: CancellationToken.None);
        await fast.QueueJobAsync(ct: CancellationToken.None);
        await third.QueueJobAsync(ct: CancellationToken.None);

        await RefillTestCommandHandler.FastStarted.WaitAsync(TimeSpan.FromSeconds(5));

        var thirdStartedBeforeSlowFinished = await Task.WhenAny(
                                                 RefillTestCommandHandler.ThirdStarted,
                                                 Task.Delay(TimeSpan.FromSeconds(1))) ==
                                             RefillTestCommandHandler.ThirdStarted;

        thirdStartedBeforeSlowFinished.ShouldBeTrue();
        storage.MaxActiveExecutions.ShouldBe(2);
        storage.GetRequestedLimitsSnapshot().ShouldContain(2);

        RefillTestCommandHandler.ReleaseSlow();

        var allCompleted = await Task.WhenAll(
                               storage.WaitForCompletionAsync(slow.TrackingID, TimeSpan.FromSeconds(5)),
                               storage.WaitForCompletionAsync(fast.TrackingID, TimeSpan.FromSeconds(5)),
                               storage.WaitForCompletionAsync(third.TrackingID, TimeSpan.FromSeconds(5)));

        allCompleted.All(static completed => completed).ShouldBeTrue();
        appStopping.Cancel();
    }

    [Fact]
    public async Task executor_waits_for_in_flight_jobs_to_finish_during_shutdown()
    {
        RefillTestCommandHandler.Reset();
        var storage = new RefillTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateRefillQueue(storage, appStopping);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20));

        var drain = new RefillTestCommand { Name = "drain", Sequence = 0 };
        await drain.QueueJobAsync(ct: CancellationToken.None);
        await RefillTestCommandHandler.DrainStarted.WaitAsync(TimeSpan.FromSeconds(5));

        appStopping.Cancel();
        await Task.Delay(200);

        queue.ExecutorTask.IsCompleted.ShouldBeFalse();

        RefillTestCommandHandler.ReleaseDrain();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));

        queue.ExecutorTask.IsCompletedSuccessfully.ShouldBeTrue();
        (await storage.WaitForCompletionAsync(drain.TrackingID, TimeSpan.FromSeconds(5))).ShouldBeTrue();
    }

    [Fact]
    public async Task executor_drains_in_flight_jobs_when_shutdown_occurs_during_semaphore_wait()
    {
        RefillTestCommandHandler.Reset();
        var storage = new RefillTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateRefillQueue(storage, appStopping);
        queue.SetLimits(2, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20));

        var drain = new RefillTestCommand { Name = "drain", Sequence = 0 };
        await drain.QueueJobAsync(ct: CancellationToken.None);
        await RefillTestCommandHandler.DrainStarted.WaitAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(200);
        appStopping.Cancel();
        await Task.Delay(200);

        queue.ExecutorTask.IsCompleted.ShouldBeFalse();

        RefillTestCommandHandler.ReleaseDrain();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));

        queue.ExecutorTask.IsCompletedSuccessfully.ShouldBeTrue();
        (await storage.WaitForCompletionAsync(drain.TrackingID, TimeSpan.FromSeconds(5))).ShouldBeTrue();
    }

    [Fact]
    public async Task distributed_executor_only_claims_available_slots_when_refilling()
    {
        DistributedRefillCommandHandler.Reset();
        var storage = new DistributedRefillStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateDistributedRefillQueue(storage, appStopping);
        queue.SetLimits(2, TimeSpan.FromMinutes(2), TimeSpan.FromMilliseconds(20));

        var slow = new DistributedRefillCommand { Name = "slow", Sequence = 0 };
        var fast = new DistributedRefillCommand { Name = "fast", Sequence = 1 };
        var third = new DistributedRefillCommand { Name = "third", Sequence = 2 };
        var fourth = new DistributedRefillCommand { Name = "fourth", Sequence = 3 };

        await slow.QueueJobAsync(ct: CancellationToken.None);
        await fast.QueueJobAsync(ct: CancellationToken.None);
        await third.QueueJobAsync(ct: CancellationToken.None);
        await fourth.QueueJobAsync(ct: CancellationToken.None);

        await DistributedRefillCommandHandler.FastStarted.WaitAsync(TimeSpan.FromSeconds(5));
        await DistributedRefillCommandHandler.ThirdStarted.WaitAsync(TimeSpan.FromSeconds(5));

        var fourthStartedEarly = await Task.WhenAny(
                                     DistributedRefillCommandHandler.FourthStarted,
                                     Task.Delay(TimeSpan.FromMilliseconds(300))) ==
                                 DistributedRefillCommandHandler.FourthStarted;

        fourthStartedEarly.ShouldBeFalse();
        storage.GetRequestedLimitsSnapshot().ShouldContain(1);
        storage.GetDequeueAfter(fourth.TrackingID).ShouldBe(DateTime.MinValue);

        DistributedRefillCommandHandler.ReleaseThird();
        await DistributedRefillCommandHandler.FourthStarted.WaitAsync(TimeSpan.FromSeconds(5));

        storage.GetDequeueAfter(fourth.TrackingID).ShouldBeGreaterThan(DateTime.UtcNow.AddSeconds(-5));

        DistributedRefillCommandHandler.ReleaseSlow();

        var allCompleted = await Task.WhenAll(
                               storage.WaitForCompletionAsync(slow.TrackingID, TimeSpan.FromSeconds(5)),
                               storage.WaitForCompletionAsync(fast.TrackingID, TimeSpan.FromSeconds(5)),
                               storage.WaitForCompletionAsync(third.TrackingID, TimeSpan.FromSeconds(5)),
                               storage.WaitForCompletionAsync(fourth.TrackingID, TimeSpan.FromSeconds(5)));

        allCompleted.All(static completed => completed).ShouldBeTrue();
        appStopping.Cancel();
    }
}
