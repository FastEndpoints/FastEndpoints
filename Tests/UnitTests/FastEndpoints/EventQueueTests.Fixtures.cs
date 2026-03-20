using FastEndpoints;
using Grpc.Core;
using static QueueTesting.QueueTestSupport;

namespace EventQueue;

public partial class EventQueueTests
{
    class TestEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class ReconnectWindowEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class ExplicitSubscriberIdEvent : IEvent;

    class ExplicitSubscriberIdHandler : IEventHandler<ExplicitSubscriberIdEvent>
    {
        public Task HandleAsync(ExplicitSubscriberIdEvent evnt, CancellationToken ct)
            => Task.CompletedTask;
    }

    class DerivedSubscriberIdEvent : IEvent;

    class DerivedSubscriberIdHandler : IEventHandler<DerivedSubscriberIdEvent>
    {
        public Task HandleAsync(DerivedSubscriberIdEvent evnt, CancellationToken ct)
            => Task.CompletedTask;
    }

    class KnownSubscriberEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class ConfiguredSubscriberEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class StaleSubscriberEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class RoundRobinReconnectRaceEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class StaleReconnectRaceEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class TrackedTestEvent : IEvent
    {
        public string Name { get; set; } = null!;
    }

    class StreamFailureEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class InMemFetchLimitEvent : IEvent
    {
        public string Name { get; set; } = null!;
    }

    class WaitRecoveryEvent : IEvent
    {
        public int EventID { get; set; }
    }

    class PollDrainEvent : IEvent
    {
        public int EventID { get; set; }
    }

    sealed class TestEventRecord : IEventStorageRecord
    {
        public string SubscriberID { get; set; } = null!;
        public Guid TrackingID { get; set; }
        public object Event { get; set; } = null!;
        public string EventType { get; set; } = null!;
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
    }

    static readonly Dictionary<string, int> _trackedEventOrder = new(StringComparer.Ordinal)
    {
        ["slow"] = 0,
        ["fast"] = 1,
        ["third"] = 2,
        ["retry"] = 3,
        ["shutdown"] = 4
    };

    static string GetTrackedEventKey(TestEventRecord record)
        => ((TrackedTestEvent)record.Event).Name;

    static int GetTrackedEventOrder(TestEventRecord record)
        => _trackedEventOrder.TryGetValue(GetTrackedEventKey(record), out var order) ? order : int.MaxValue;

    sealed class TestEventSubscriberStorage : TestEventSubscriberStorageBase<TestEventRecord>
    {
        public TestEventSubscriberStorage()
            : base(GetTrackedEventKey, GetTrackedEventOrder) { }

        public int GetExecutionCount(string eventName)
            => GetExecutionCountCore(eventName);

        public bool AllCompleted(params string[] eventNames)
            => AllCompletedCore(eventNames);
    }

    abstract class TestEventSubscriberStorageBase<TRecord> : IEventSubscriberStorageProvider<TRecord> where TRecord : class, IEventStorageRecord
    {
        readonly object _sync = new();
        readonly List<TRecord> _records = [];
        readonly Dictionary<string, int> _executionCounts = new(StringComparer.Ordinal);
        readonly List<int> _requestedLimits = [];
        readonly Func<TRecord, string> _keySelector;
        readonly Func<TRecord, int> _orderSelector;
        int _activeExecutions;

        protected TestEventSubscriberStorageBase(Func<TRecord, string> keySelector, Func<TRecord, int> orderSelector)
        {
            _keySelector = keySelector;
            _orderSelector = orderSelector;
        }

        public int MaxConcurrentExecutions { get; private set; }

        public ValueTask StoreEventAsync(TRecord record, CancellationToken ct)
        {
            lock (_sync)
                _records.Add(record);

            return default;
        }

        public ValueTask<IEnumerable<TRecord>> GetNextBatchAsync(PendingRecordSearchParams<TRecord> parameters)
        {
            var match = parameters.Match.Compile();

            lock (_sync)
            {
                _requestedLimits.Add(parameters.Limit);

                var batch = _records.Where(match)
                                    .OrderBy(_orderSelector)
                                    .Take(parameters.Limit)
                                    .ToArray();

                return new(batch);
            }
        }

        public virtual ValueTask MarkEventAsCompleteAsync(TRecord record, CancellationToken ct)
        {
            record.IsComplete = true;

            return default;
        }

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<TRecord> parameters)
            => default;

        public IReadOnlyList<int> GetRequestedLimitsSnapshot()
        {
            lock (_sync)
                return _requestedLimits.ToArray();
        }

        public void OnExecutionStarted(string key)
        {
            lock (_sync)
            {
                _activeExecutions++;
                MaxConcurrentExecutions = Math.Max(MaxConcurrentExecutions, _activeExecutions);
                _executionCounts.TryGetValue(key, out var count);
                _executionCounts[key] = count + 1;
            }
        }

        public void OnExecutionCompleted()
        {
            lock (_sync)
                _activeExecutions--;
        }

        protected int GetExecutionCountCore(string key)
        {
            lock (_sync)
                return _executionCounts.TryGetValue(key, out var count) ? count : 0;
        }

        protected bool AllCompletedCore(params string[] keys)
        {
            lock (_sync)
            {
                return keys.All(
                    key =>
                        _records.Any(
                            record =>
                                record.IsComplete &&
                                string.Equals(_keySelector(record), key, StringComparison.Ordinal)));
            }
        }
    }

    sealed class CancellationAwareTestEventSubscriberStorage : TestEventSubscriberStorageBase<TestEventRecord>
    {
        public CancellationAwareTestEventSubscriberStorage()
            : base(GetTrackedEventKey, GetTrackedEventOrder) { }

        public TaskCompletionSource MarkCompleteObserved { get; } = NewSignal();
        public bool MarkCompleteTokenCanBeCanceled { get; private set; }

        public bool AllCompleted(params string[] eventNames)
            => AllCompletedCore(eventNames);

        public int GetExecutionCount(string eventName)
            => GetExecutionCountCore(eventName);

        public override ValueTask MarkEventAsCompleteAsync(TestEventRecord record, CancellationToken ct)
        {
            MarkCompleteTokenCanBeCanceled = ct.CanBeCanceled;

            if (ct.IsCancellationRequested)
                return ValueTask.FromException(new OperationCanceledException(ct));

            record.IsComplete = true;
            MarkCompleteObserved.TrySetResult();

            return default;
        }
    }

    sealed class CancellationAwareStoreEventSubscriberStorage(CancellationTokenSource shutdownCts) : IEventSubscriberStorageProvider<TestEventRecord>
    {
        public TaskCompletionSource StoreObserved { get; } = NewSignal();
        public bool StoreTokenCanBeCanceled { get; private set; }
        public List<TestEventRecord> StoredRecords { get; } = [];

        public ValueTask StoreEventAsync(TestEventRecord record, CancellationToken ct)
        {
            StoreTokenCanBeCanceled = ct.CanBeCanceled;

            if (ct.IsCancellationRequested)
                return ValueTask.FromException(new OperationCanceledException(ct));

            StoredRecords.Add(record);
            StoreObserved.TrySetResult();
            shutdownCts.Cancel();

            return default;
        }

        public ValueTask<IEnumerable<TestEventRecord>> GetNextBatchAsync(PendingRecordSearchParams<TestEventRecord> parameters)
            => new(Array.Empty<TestEventRecord>());

        public ValueTask MarkEventAsCompleteAsync(TestEventRecord record, CancellationToken ct)
            => default;

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<TestEventRecord> parameters)
            => default;
    }

    sealed class EventHubStoreObserver
    {
        public TaskCompletionSource StoreObserved { get; } = NewSignal();
        public bool StoreTokenCanBeCanceled { get; set; }
        public List<TestEventRecord> StoredRecords { get; } = [];
    }

    sealed class CancellationAwareEventHubStorage(CancellationTokenSource shutdownCts, EventHubStoreObserver observer) : IEventHubStorageProvider<TestEventRecord>
    {
        public ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<TestEventRecord> parameters)
            => new(Array.Empty<string>());

        public ValueTask StoreEventsAsync(IEnumerable<TestEventRecord> records, CancellationToken ct)
        {
            observer.StoreTokenCanBeCanceled = ct.CanBeCanceled;

            if (ct.IsCancellationRequested)
                return ValueTask.FromException(new OperationCanceledException(ct));

            observer.StoredRecords.AddRange(records.Select(Clone));
            observer.StoreObserved.TrySetResult();
            shutdownCts.Cancel();

            return default;
        }

        public ValueTask<IEnumerable<TestEventRecord>> GetNextBatchAsync(PendingRecordSearchParams<TestEventRecord> parameters)
            => new(Array.Empty<TestEventRecord>());

        public ValueTask MarkEventAsCompleteAsync(TestEventRecord record, CancellationToken ct)
            => default;

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<TestEventRecord> parameters)
            => default;

        static TestEventRecord Clone(TestEventRecord record)
            => new()
            {
                SubscriberID = record.SubscriberID,
                TrackingID = record.TrackingID,
                Event = record.Event,
                EventType = record.EventType,
                ExpireOn = record.ExpireOn,
                IsComplete = record.IsComplete
            };
    }

    class TestEventExecutorHandler(TestEventSubscriberStorage storage) : IEventHandler<TrackedTestEvent>
    {
        internal static TaskCompletionSource FastStarted { get; private set; } = NewSignal();
        internal static TaskCompletionSource ThirdStarted { get; private set; } = NewSignal();
        internal static TaskCompletionSource RetryObserved { get; private set; } = NewSignal();
        static TaskCompletionSource _slowCanFinish = NewSignal();
        static TaskCompletionSource _thirdCanFinish = NewSignal();
        static TaskCompletionSource _retryCanSucceed = NewSignal();
        static int _retryAttempts;

        public static void Reset()
        {
            FastStarted = NewSignal();
            ThirdStarted = NewSignal();
            RetryObserved = NewSignal();
            _slowCanFinish = NewSignal();
            _thirdCanFinish = NewSignal();
            _retryCanSucceed = NewSignal();
            _retryAttempts = 0;
        }

        public static void ReleaseSlow()
            => _slowCanFinish.TrySetResult();

        public static void ReleaseThird()
            => _thirdCanFinish.TrySetResult();

        public static void ReleaseRetry()
            => _retryCanSucceed.TrySetResult();

        public async Task HandleAsync(TrackedTestEvent evnt, CancellationToken ct)
        {
            storage.OnExecutionStarted(evnt.Name);

            try
            {
                switch (evnt.Name)
                {
                    case "slow":
                        await _slowCanFinish.Task.WaitAsync(ct);
                        break;
                    case "fast":
                        FastStarted.TrySetResult();
                        break;
                    case "third":
                        ThirdStarted.TrySetResult();
                        await _thirdCanFinish.Task.WaitAsync(ct);
                        break;
                    case "retry":
                        if (Interlocked.Increment(ref _retryAttempts) == 1)
                        {
                            RetryObserved.TrySetResult();
                            throw new InvalidOperationException("boom");
                        }

                        await _retryCanSucceed.Task.WaitAsync(ct);
                        break;
                }
            }
            finally
            {
                storage.OnExecutionCompleted();
            }
        }
    }

    sealed class ShutdownAfterHandleEventHandler(CancellationAwareTestEventSubscriberStorage storage, CancellationTokenSource shutdownCts)
        : IEventHandler<TrackedTestEvent>
    {
        public Task HandleAsync(TrackedTestEvent evnt, CancellationToken ct)
        {
            storage.OnExecutionStarted(evnt.Name);

            try
            {
                shutdownCts.Cancel();
                return Task.CompletedTask;
            }
            finally
            {
                storage.OnExecutionCompleted();
            }
        }
    }

    sealed class ThrowingSubscriberExceptionReceiver : SubscriberExceptionReceiver
    {
        public override Task OnHandlerExecutionError<TEvent, THandler>(IEventStorageRecord record,
                                                                       int attemptCount,
                                                                       Exception exception,
                                                                       CancellationToken ct)
            => throw new InvalidOperationException("receiver failure");
    }

    sealed class GateServerStreamWriter<T> : IServerStreamWriter<T>
    {
        readonly TaskCompletionSource _gate = NewSignal();

        public WriteOptions? WriteOptions { get; set; }
        public List<T> Responses { get; } = new();

        public void Release()
            => _gate.TrySetResult();

        public async Task WriteAsync(T message)
        {
            await _gate.Task;
            Responses.Add(message);
        }

        public Task WriteAsync(T message, CancellationToken ct)
            => WriteAsync(message);
    }

    sealed class TestCallInvoker(TrackedTestEvent eventMessage) : CallInvoker
    {
        readonly AsyncServerStreamingCall<TrackedTestEvent> _call = new(
            new SingleMessageAsyncStreamReader<TrackedTestEvent>(eventMessage),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new(),
            () => { });

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                                                    string? host,
                                                                                                                    CallOptions options)
            => throw new NotSupportedException();

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                                                    string? host,
                                                                                                                    CallOptions options)
            => throw new NotSupportedException();

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                                          string? host,
                                                                                                          CallOptions options,
                                                                                                          TRequest request)
            => (AsyncServerStreamingCall<TResponse>)(object)_call;

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                      string? host,
                                                                                      CallOptions options,
                                                                                      TRequest request)
            => throw new NotSupportedException();

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                         string? host,
                                                                         CallOptions options,
                                                                         TRequest request)
            => throw new NotSupportedException();
    }

    sealed class GracefulReconnectCallInvoker(CancellationTokenSource shutdownCts, int expectedCalls) : CallInvoker
    {
        int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);
        public TaskCompletionSource ExpectedCallCountReached { get; } = NewSignal();

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                                                    string? host,
                                                                                                                    CallOptions options)
            => throw new NotSupportedException();

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                                                    string? host,
                                                                                                                    CallOptions options)
            => throw new NotSupportedException();

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                                          string? host,
                                                                                                          CallOptions options,
                                                                                                          TRequest request)
        {
            if (Interlocked.Increment(ref _callCount) == expectedCalls)
            {
                ExpectedCallCountReached.TrySetResult();
                shutdownCts.Cancel();
            }

            var call = new AsyncServerStreamingCall<TrackedTestEvent>(
                new EmptyAsyncStreamReader<TrackedTestEvent>(),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new(),
                () => { });

            return (AsyncServerStreamingCall<TResponse>)(object)call;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                      string? host,
                                                                                      CallOptions options,
                                                                                      TRequest request)
            => throw new NotSupportedException();

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                         string? host,
                                                                         CallOptions options,
                                                                         TRequest request)
            => throw new NotSupportedException();
    }

    sealed class SingleMessageAsyncStreamReader<T>(T message) : IAsyncStreamReader<T>
    {
        bool _moved;

        public T Current { get; private set; } = default!;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (_moved)
                return Task.FromResult(false);

            _moved = true;
            Current = message;

            return Task.FromResult(true);
        }
    }

    sealed class EmptyAsyncStreamReader<T> : IAsyncStreamReader<T>
    {
        public T Current => default!;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
            => Task.FromResult(false);
    }

    class FailingServerStreamWriter<T>(int failAfter) : IServerStreamWriter<T>
    {
        int _writeCount;

        public WriteOptions? WriteOptions { get; set; }
        public List<T> Responses { get; } = new();

        public Task WriteAsync(T message)
        {
            if (_writeCount >= failAfter)
                throw new InvalidOperationException("Simulated stream failure");

            _writeCount++;
            Responses.Add(message);

            return Task.CompletedTask;
        }

        public Task WriteAsync(T message, CancellationToken ct)
            => WriteAsync(message);
    }

    sealed class BatchDequeueEventHubStorage : IEventHubStorageProvider<TestEventRecord>
    {
        readonly object _sync = new();
        readonly Queue<TestEventRecord> _queue = new();

        public void Enqueue(TestEventRecord record)
        {
            lock (_sync)
                _queue.Enqueue(record);
        }

        public int QueueCount
        {
            get
            {
                lock (_sync)
                    return _queue.Count;
            }
        }

        public ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<TestEventRecord> parameters)
            => new(Array.Empty<string>());

        public ValueTask StoreEventsAsync(IEnumerable<TestEventRecord> records, CancellationToken ct)
        {
            lock (_sync)
            {
                foreach (var record in records)
                    _queue.Enqueue(record);
            }

            return default;
        }

        public ValueTask<IEnumerable<TestEventRecord>> GetNextBatchAsync(PendingRecordSearchParams<TestEventRecord> parameters)
        {
            lock (_sync)
            {
                var batch = new List<TestEventRecord>();

                while (batch.Count < parameters.Limit && _queue.Count > 0)
                    batch.Add(_queue.Dequeue());

                return new(batch);
            }
        }

        public ValueTask MarkEventAsCompleteAsync(TestEventRecord record, CancellationToken ct)
            => default;

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<TestEventRecord> parameters)
            => default;
    }

    sealed class InstrumentedEventHubStorageState
    {
        readonly object _sync = new();
        readonly List<TestEventRecord> _records = [];

        public TaskCompletionSource SecondFetchObserved { get; } = NewSignal();
        public int EmptyBatchCount { get; private set; }
        public int GetNextBatchCallCount { get; private set; }

        public void Store(IEnumerable<TestEventRecord> records)
        {
            lock (_sync)
                _records.AddRange(records);
        }

        public IReadOnlyList<TestEventRecord> GetNextBatch(PendingRecordSearchParams<TestEventRecord> parameters)
        {
            var match = parameters.Match.Compile();

            lock (_sync)
            {
                GetNextBatchCallCount++;

                if (GetNextBatchCallCount >= 2)
                    SecondFetchObserved.TrySetResult();

                var batch = _records.Where(match)
                                    .Take(parameters.Limit)
                                    .ToArray();

                if (batch.Length == 0)
                    EmptyBatchCount++;

                return batch;
            }
        }

        public void MarkComplete(TestEventRecord record)
        {
            lock (_sync)
                record.IsComplete = true;
        }
    }

    sealed class InstrumentedEventHubStorage(InstrumentedEventHubStorageState state) : IEventHubStorageProvider<TestEventRecord>
    {
        public ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<TestEventRecord> parameters)
            => new(Array.Empty<string>());

        public ValueTask StoreEventsAsync(IEnumerable<TestEventRecord> records, CancellationToken ct)
        {
            state.Store(records);
            return default;
        }

        public ValueTask<IEnumerable<TestEventRecord>> GetNextBatchAsync(PendingRecordSearchParams<TestEventRecord> parameters)
            => new(state.GetNextBatch(parameters));

        public ValueTask MarkEventAsCompleteAsync(TestEventRecord record, CancellationToken ct)
        {
            state.MarkComplete(record);
            return default;
        }

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<TestEventRecord> parameters)
            => default;
    }

    sealed class BatchDequeueEventSubscriberStorage : IEventSubscriberStorageProvider<TestEventRecord>
    {
        readonly object _sync = new();
        readonly Queue<TestEventRecord> _queue = new();
        readonly List<int> _requestedLimits = [];

        public ValueTask StoreEventAsync(TestEventRecord record, CancellationToken ct)
        {
            lock (_sync)
                _queue.Enqueue(record);

            return default;
        }

        public ValueTask<IEnumerable<TestEventRecord>> GetNextBatchAsync(PendingRecordSearchParams<TestEventRecord> parameters)
        {
            lock (_sync)
            {
                _requestedLimits.Add(parameters.Limit);

                var batch = new List<TestEventRecord>();

                while (batch.Count < parameters.Limit && _queue.Count > 0)
                    batch.Add(_queue.Dequeue());

                return new(batch);
            }
        }

        public ValueTask MarkEventAsCompleteAsync(TestEventRecord record, CancellationToken ct)
            => default;

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<TestEventRecord> parameters)
            => default;

        public IReadOnlyList<int> GetRequestedLimitsSnapshot()
        {
            lock (_sync)
                return [.. _requestedLimits];
        }
    }

    class InMemFetchLimitHandler : IEventHandler<InMemFetchLimitEvent>
    {
        static int _processedCount;
        static TaskCompletionSource _slowGate = NewSignal();

        public static void Reset()
        {
            Interlocked.Exchange(ref _processedCount, 0);
            _slowGate = NewSignal();
        }

        public static void ReleaseSlow()
            => _slowGate.TrySetResult();

        public static int ProcessedCount => Volatile.Read(ref _processedCount);

        public async Task HandleAsync(InMemFetchLimitEvent evnt, CancellationToken ct)
        {
            if (evnt.Name == "slow")
                await _slowGate.Task.WaitAsync(ct);

            Interlocked.Increment(ref _processedCount);
        }
    }
}
