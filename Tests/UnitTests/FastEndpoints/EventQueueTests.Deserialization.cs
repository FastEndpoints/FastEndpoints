using FastEndpoints;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using QueueTesting;
using Xunit;
using static QueueTesting.QueueTestSupport;

namespace EventQueue;

public partial class EventQueueTests
{
    [Fact]
    public async Task event_deserialization_recovers_before_retry_limit()
    {
        var record = new DeserializationRecord { FailuresRemaining = 2 };
        var receiver = new DeserializationReceiver();
        var ctx = new HubContext(NullLogger.Instance, receiver, typeof(TestEvent).FullName!, CancellationToken.None);

        var evnt = await ctx.DeserializeEvent<TestEvent>(record, CancellationToken.None, TimeSpan.Zero);

        evnt.ShouldBeSameAs(record.Event);
        record.ReadAttempts.ShouldBe(3);
        receiver.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task event_deserialization_cancellation_during_retry_preserves_record()
    {
        using var cts = new CancellationTokenSource();
        var record = new DeserializationRecord { FailuresRemaining = 3 };
        var receiver = new DeserializationReceiver();
        var ctx = new HubContext(NullLogger.Instance, receiver, typeof(TestEvent).FullName!, cts.Token);
        var read = ctx.DeserializeEvent<TestEvent>(record, cts.Token, TimeSpan.FromMinutes(1)).AsTask();

        record.ReadAttempts.ShouldBe(1);
        cts.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(read);
        receiver.Calls.ShouldBe(0);
        record.IsComplete.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task event_deserialization_terminal_failure_notifies_before_completion_and_continues(bool ack, bool throwingReceiver)
    {
        var poison = new DeserializationRecord { FailuresRemaining = 3 };
        var good = new DeserializationRecord();
        var storage = new DeserializationStorage(poison, good);
        var receiver = new DeserializationReceiver { Throw = throwingReceiver };
        var registry = new SubscriberRegistry();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ctx = new HubContext(NullLogger.Instance, receiver, typeof(TestEvent).FullName!, CancellationToken.None);
        Task call;
        var ordinaryWriter = new TestServerStreamWriter<TestEvent>();
        var ackWriter = new TestServerStreamWriter<EventDelivery<TestEvent>>();

        if (ack)
        {
            var reader = new PendingAckReader();
            reader.Send(new() { TrackingID = good.TrackingID });
            call = EventDeliveryAckDispatcher.RunAsync<TestEvent, DeserializationRecord, DeserializationStorage>(
                storage, registry, ctx, poison.SubscriberID, reader, ackWriter, cts.Token, deserializationRetryDelay: TimeSpan.Zero);
        }
        else
            call = EventDispatcherWorker.RunAsync<TestEvent, DeserializationRecord, DeserializationStorage>(
                storage, HubStorageBehavior.Durable, registry, ctx, poison.SubscriberID, ordinaryWriter, cts.Token, deserializationRetryDelay: TimeSpan.Zero);

        try
        {
            await receiver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            poison.ReadAttempts.ShouldBe(3);
            poison.IsComplete.ShouldBeFalse();
            storage.Completions.ShouldBe(0);
            receiver.Record.ShouldBeSameAs(poison);
            receiver.Exception.ShouldBeSameAs(poison.Error);
            receiver.Attempts.ShouldBe(3);
            receiver.Release.TrySetResult();
            await WaitUntil(() => storage.Completions == 2, timeoutMs: 5000);
            receiver.Calls.ShouldBe(1);
            poison.IsComplete.ShouldBeTrue();
            good.IsComplete.ShouldBeTrue();

            if (ack)
            {
                ackWriter.Responses.Count.ShouldBe(1);
                ackWriter.Responses.Single().TrackingID.ShouldBe(good.TrackingID);
            }
            else
            {
                ordinaryWriter.Responses.Count.ShouldBe(1);
                ordinaryWriter.Responses.Single().ShouldBeSameAs(good.Event);
            }
        }
        finally
        {
            receiver.Release.TrySetResult();
            cts.Cancel();
            await call;
            registry.Subscribers[poison.SubscriberID].ConnectionCount.ShouldBe(0);
        }
    }

    [Fact]
    public async Task event_deserialization_without_receiver_returns_terminal_failure()
    {
        var record = new DeserializationRecord { FailuresRemaining = 3 };
        var ctx = new HubContext(NullLogger.Instance, null, typeof(TestEvent).FullName!, CancellationToken.None);

        (await ctx.DeserializeEvent<TestEvent>(record, CancellationToken.None, TimeSpan.Zero)).ShouldBeNull();
        record.ReadAttempts.ShouldBe(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task event_deserialization_cancellation_during_callback_leaves_durable_row_pending(bool ack)
    {
        var poison = new DeserializationRecord { FailuresRemaining = 3 };
        var storage = new DeserializationStorage(poison);
        var receiver = new DeserializationReceiver();
        var registry = new SubscriberRegistry();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ctx = new HubContext(NullLogger.Instance, receiver, typeof(TestEvent).FullName!, CancellationToken.None);
        Task call;

        if (ack)
        {
            call = EventDeliveryAckDispatcher.RunAsync<TestEvent, DeserializationRecord, DeserializationStorage>(
                storage, registry, ctx, poison.SubscriberID,
                new PendingAckReader(), new TestServerStreamWriter<EventDelivery<TestEvent>>(), cts.Token, deserializationRetryDelay: TimeSpan.Zero);
        }
        else
            call = EventDispatcherWorker.RunAsync<TestEvent, DeserializationRecord, DeserializationStorage>(
                storage, HubStorageBehavior.Durable, registry, ctx, poison.SubscriberID,
                new TestServerStreamWriter<TestEvent>(), cts.Token, deserializationRetryDelay: TimeSpan.Zero);

        try
        {
            await receiver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cts.Cancel();
            await call;
            poison.IsComplete.ShouldBeFalse();
            storage.Completions.ShouldBe(0);
            receiver.Calls.ShouldBe(1);

            registry.Subscribers[poison.SubscriberID].ConnectionCount.ShouldBe(0);
        }
        finally
        {
            cts.Cancel();
            receiver.Release.TrySetResult();
            await call;
        }
    }

    [Fact]
    public async Task event_deserialization_cancellation_requeues_remaining_in_memory_batch()
    {
        var delivered = new DeserializationRecord();
        var retrying = new DeserializationRecord { FailuresRemaining = 3 };
        var remaining = new DeserializationRecord();
        var storage = new InMemoryDeserializationStorage(delivered, retrying, remaining);
        var receiver = new DeserializationReceiver();
        var registry = new SubscriberRegistry();
        var writer = new TestServerStreamWriter<TestEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ctx = new HubContext(NullLogger.Instance, receiver, typeof(TestEvent).FullName!, CancellationToken.None);
        var call = EventDispatcherWorker.RunAsync<TestEvent, DeserializationRecord, InMemoryDeserializationStorage>(
            storage, HubStorageBehavior.InMemory, registry, ctx, retrying.SubscriberID, writer, cts.Token,
            deserializationRetryDelay: TimeSpan.FromMinutes(1));

        try
        {
            await WaitUntil(() => retrying.ReadAttempts == 1, timeoutMs: 5000);
            writer.Responses.Single().ShouldBeSameAs(delivered.Event);
            remaining.ReadAttempts.ShouldBe(0);
            storage.Records.Count.ShouldBe(0);
            registry.Subscribers[retrying.SubscriberID].ConnectionCount.ShouldBe(1);

            cts.Cancel();
            await call.WaitAsync(TimeSpan.FromSeconds(5));

            storage.StoreCalls.ShouldBe(1);
            storage.StoreToken.IsCancellationRequested.ShouldBeFalse();
            storage.StoreToken.ShouldBe(CancellationToken.None);
            storage.Records.ToArray().ShouldBe(new[] { retrying, remaining });
            retrying.ReadAttempts.ShouldBe(1);
            retrying.IsComplete.ShouldBeFalse();
            remaining.ReadAttempts.ShouldBe(0);
            remaining.IsComplete.ShouldBeFalse();
            receiver.Calls.ShouldBe(0);
            registry.Subscribers[retrying.SubscriberID].ConnectionCount.ShouldBe(0);
        }
        finally
        {
            cts.Cancel();
            await call.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    sealed class InMemoryDeserializationStorage(params DeserializationRecord[] records) : IEventHubStorageProvider<DeserializationRecord>
    {
        internal readonly Queue<DeserializationRecord> Records = new(records);
        internal int StoreCalls;
        internal CancellationToken StoreToken;

        public ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<DeserializationRecord> parameters)
            => new(Array.Empty<string>());

        public ValueTask StoreEventsAsync(IEnumerable<DeserializationRecord> r, CancellationToken ct)
        {
            StoreCalls++;
            StoreToken = ct;
            ct.ThrowIfCancellationRequested();

            foreach (var record in r)
                Records.Enqueue(record);

            return ValueTask.CompletedTask;
        }

        public ValueTask<IEnumerable<DeserializationRecord>> GetNextBatchAsync(PendingRecordSearchParams<DeserializationRecord> parameters)
        {
            List<DeserializationRecord> batch = [];

            while (batch.Count < parameters.Limit && Records.TryDequeue(out var record))
                batch.Add(record);

            return new(batch);
        }

        public ValueTask MarkEventAsCompleteAsync(DeserializationRecord r, CancellationToken ct)
            => throw new InvalidOperationException("in-memory records are dequeued on read");

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<DeserializationRecord> parameters)
            => ValueTask.CompletedTask;
    }

    sealed class DeserializationRecord : IEventStorageRecord
    {
        public string SubscriberID { get; set; } = "deserialization-sub";
        public Guid TrackingID { get; set; } = Guid.NewGuid();
        public object Event { get; set; } = new TestEvent();
        public string EventType { get; set; } = typeof(TestEvent).FullName!;
        public DateTime ExpireOn { get; set; } = DateTime.UtcNow.AddHours(1);
        public bool IsComplete { get; set; }
        internal int FailuresRemaining;
        internal int ReadAttempts;
        internal readonly Exception Error = new InvalidOperationException("decoder unavailable");

        public TEvent GetEvent<TEvent>() where TEvent : IEvent
        {
            ReadAttempts++;

            if (FailuresRemaining-- > 0)
                throw Error;

            return (TEvent)Event;
        }
    }

    sealed class DeserializationReceiver : EventHubExceptionReceiver
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal IEventStorageRecord? Record;
        internal Exception? Exception;
        internal int Attempts;
        internal int Calls;
        internal bool Throw;

        public override async Task OnDeserializeEventError<TEvent>(IEventStorageRecord record, int attemptCount, Exception exception, CancellationToken ct)
        {
            Record = record;
            Exception = exception;
            Attempts = attemptCount;
            Calls++;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(ct);

            if (Throw)
                throw new InvalidOperationException("DLQ unavailable");
        }
    }

    sealed class DeserializationStorage(params DeserializationRecord[] records) : IEventHubStorageProvider<DeserializationRecord>
    {
        internal int Completions;

        public ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<DeserializationRecord> parameters)
            => new(Array.Empty<string>());

        public ValueTask StoreEventsAsync(IEnumerable<DeserializationRecord> r, CancellationToken ct)
            => ValueTask.CompletedTask;

        public ValueTask<IEnumerable<DeserializationRecord>> GetNextBatchAsync(PendingRecordSearchParams<DeserializationRecord> parameters)
            => new(records.Where(parameters.Match.Compile()).Take(parameters.Limit).ToArray());

        public ValueTask MarkEventAsCompleteAsync(DeserializationRecord r, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Interlocked.Increment(ref Completions);

            return ValueTask.CompletedTask;
        }

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<DeserializationRecord> parameters)
            => ValueTask.CompletedTask;
    }
}
