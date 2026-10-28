using FastEndpoints;
using Microsoft.Extensions.Logging.Abstractions;
using QueueTesting;
using Xunit;
using static QueueTesting.QueueTestSupport;

namespace EventQueue;

public partial class EventQueueTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task hub_retrieval_retries_recovers_resets_and_cancels(bool ack, bool throwingReceiver)
        => ack
               ? VerifyRetrieval<AckRetrievalEvent>(ack, throwingReceiver)
               : VerifyRetrieval<OrdinaryRetrievalEvent>(ack, throwingReceiver);

    static async Task VerifyRetrieval<TEvent>(bool ack, bool throwingReceiver) where TEvent : class, IEvent, new()
    {
        using var connection = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var app = new CancellationTokenSource();
        var storage = new RetrievalStorage<TEvent>();
        var receiver = new RetrievalReceiver { Throw = throwingReceiver };
        var registry = new SubscriberRegistry();
        var ctx = new HubContext(NullLogger.Instance, receiver, typeof(TEvent).FullName!, app.Token);
        var ordinaryWriter = new TestServerStreamWriter<TEvent>();
        var ackWriter = new TestServerStreamWriter<EventDelivery<TEvent>>();
        Task call;

        if (ack)
        {
            var reader = new PendingAckReader();

            foreach (var record in storage.Records)
                reader.Send(new() { TrackingID = record.TrackingID });

            call = EventDeliveryAckDispatcher.RunAsync<TEvent, RetrievalRecord<TEvent>, RetrievalStorage<TEvent>>(
                storage, registry, ctx, "retrieval-sub", reader, ackWriter, connection.Token, retrievalRetryDelay: TimeSpan.Zero);
        }
        else
            call = EventDispatcherWorker.RunAsync<TEvent, RetrievalRecord<TEvent>, RetrievalStorage<TEvent>>(
                storage, HubStorageBehavior.Durable, registry, ctx, "retrieval-sub", ordinaryWriter, connection.Token, retrievalRetryDelay: TimeSpan.Zero);

        try
        {
            await storage.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(25));
            receiver.Counts.ShouldBe([1, 2, 1]);
            receiver.Exceptions.ShouldAllBe(ex => ReferenceEquals(ex, storage.Error));
            storage.Limits.ShouldAllBe(limit => limit == (ack ? 1 : EventHubSettings.BatchSize));
            storage.Completions.ShouldBe(2);
            storage.Records.ShouldAllBe(record => record.IsComplete);

            if (ack)
                ackWriter.Responses.Select(response => response.TrackingID).ShouldBe(storage.Records.Select(record => record.TrackingID));
            else
                ordinaryWriter.Responses.ShouldBe(storage.Records.Select(record => (TEvent)record.Event));

            if (throwingReceiver)
                app.Cancel();
            else
                connection.Cancel();

            await call.WaitAsync(TimeSpan.FromSeconds(5));
            storage.RetrievalCanceled.ShouldBeTrue();
            receiver.Counts.ShouldBe([1, 2, 1]);
            if (!ack)
                registry.GetConnectedSubscriberIds().ShouldBeEmpty();
        }
        finally
        {
            connection.Cancel();
            await call;

            registry.Subscribers["retrieval-sub"].ConnectionCount.ShouldBe(0);
        }
    }

    sealed class OrdinaryRetrievalEvent : IEvent;
    sealed class AckRetrievalEvent : IEvent;

    sealed class RetrievalRecord<TEvent> : IEventStorageRecord where TEvent : class, IEvent, new()
    {
        public string SubscriberID { get; set; } = "retrieval-sub";
        public Guid TrackingID { get; set; } = Guid.NewGuid();
        public object Event { get; set; } = new TEvent();
        public string EventType { get; set; } = typeof(TEvent).FullName!;
        public DateTime ExpireOn { get; set; } = DateTime.UtcNow.AddHours(1);
        public bool IsComplete { get; set; }
        public T GetEvent<T>() where T : IEvent => (T)Event;
    }

    sealed class RetrievalReceiver : EventHubExceptionReceiver
    {
        internal readonly List<int> Counts = [];
        internal readonly List<Exception> Exceptions = [];
        internal bool Throw;

        public override Task OnGetNextBatchError<TEvent>(string subscriberID, int errorCount, Exception exception, CancellationToken ct)
        {
            subscriberID.ShouldBe("retrieval-sub");
            Counts.Add(errorCount);
            Exceptions.Add(exception);

            if (Throw)
                throw new InvalidOperationException("receiver unavailable");

            return Task.CompletedTask;
        }
    }

    sealed class RetrievalStorage<TEvent> : IEventHubStorageProvider<RetrievalRecord<TEvent>> where TEvent : class, IEvent, new()
    {
        internal readonly RetrievalRecord<TEvent>[] Records = [new(), new()];
        internal readonly List<int> Limits = [];
        internal readonly Exception Error = new InvalidOperationException("storage unavailable");
        internal readonly TaskCompletionSource Blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Completions;
        internal bool RetrievalCanceled;
        int _calls;

        public async ValueTask<IEnumerable<RetrievalRecord<TEvent>>> GetNextBatchAsync(PendingRecordSearchParams<RetrievalRecord<TEvent>> parameters)
        {
            Limits.Add(parameters.Limit);
            parameters.SubscriberID.ShouldBe("retrieval-sub");
            parameters.EventType.ShouldBe(typeof(TEvent).FullName!);
            var match = parameters.Match.Compile();
            match(new() { SubscriberID = "other" }).ShouldBeFalse();
            match(new() { EventType = "other" }).ShouldBeFalse();
            match(new() { IsComplete = true }).ShouldBeFalse();
            match(new() { ExpireOn = DateTime.UtcNow.AddMinutes(-1) }).ShouldBeFalse();
            match(new()).ShouldBeTrue();

            switch (++_calls)
            {
                case 1:
                case 2:
                case 4:
                    throw Error;
                case 3:
                    return [Records[0]];
                case 5:
                    return [Records[1]];
                default:
                    Blocked.TrySetResult();

                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, parameters.CancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        RetrievalCanceled = true;
                        throw;
                    }

                    return [];
            }
        }

        public ValueTask MarkEventAsCompleteAsync(RetrievalRecord<TEvent> record, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Completions++;

            return ValueTask.CompletedTask;
        }

        public ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<RetrievalRecord<TEvent>> parameters)
            => new(Array.Empty<string>());

        public ValueTask StoreEventsAsync(IEnumerable<RetrievalRecord<TEvent>> records, CancellationToken ct)
            => ValueTask.CompletedTask;

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<RetrievalRecord<TEvent>> parameters)
            => ValueTask.CompletedTask;
    }
}
