using FastEndpoints;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using QueueTesting;
using Xunit;
using static QueueTesting.QueueTestSupport;

namespace EventQueue;

public partial class EventQueueTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task receiver_store_retry_reports_each_failure_and_isolates_callback_errors(bool ack, bool throwOnError)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var sem = new SemaphoreSlim(0);
        var invoker = new SupervisionInvoker(cts, [new(HasEvent: true), SupervisionStep.Stop()]);
        var errors = new StoreRetryErrors(throwOnError);
        var logger = new StoreRetryLogger();
        var attempts = new List<DeliveryAckRecord>();
        var failures = new[] { new IOException("store failure 1"), new IOException("store failure 2") };
        var state = new DeliveryAckSubscriberState
        {
            BeforeStore = record =>
            {
                attempts.Add(record);
                if (attempts.Count <= failures.Length)
                    throw failures[attempts.Count - 1];

                return ValueTask.CompletedTask;
            }
        };
        var storage = new DeliveryAckSubscriberStorage(state);
        var opts = new CallOptions(cancellationToken: cts.Token);
        const string subscriberId = "store-retry";
        var eventTypeName = typeof(TrackedTestEvent).FullName!;
        Task worker;
        if (ack)
        {
            var method = new Method<EventDeliveryAck, EventDelivery<TrackedTestEvent>>(
                MethodType.DuplexStreaming, eventTypeName + "/sub-ack", "",
                new MessagePackMarshaller<EventDeliveryAck>(), new MessagePackMarshaller<EventDelivery<TrackedTestEvent>>());
            worker = EventDeliveryAckReceiver.RunAsync<TrackedTestEvent, DeliveryAckRecord, DeliveryAckSubscriberStorage>(
                storage, SubscriberStorageBehavior.Durable, sem, opts, invoker, method,
                subscriberId, eventTypeName, TimeSpan.FromMinutes(1), logger, errors, TimeSpan.Zero);
        }
        else
            worker = EventReceiverWorker.RunAsync<TrackedTestEvent, DeliveryAckRecord, DeliveryAckSubscriberStorage>(
                storage, SubscriberStorageBehavior.Durable, sem, opts, invoker, CreateSubscriptionMethod<TrackedTestEvent>(),
                subscriberId, eventTypeName, TimeSpan.FromMinutes(1), logger, errors, TimeSpan.Zero);

        await worker.WaitAsync(TimeSpan.FromSeconds(3));

        attempts.Count.ShouldBe(3);
        attempts.ShouldAllBe(record => ReferenceEquals(record, attempts[0]));
        errors.Counts.ShouldBe([1, 2]);
        errors.Records.ShouldAllBe(record => ReferenceEquals(record, attempts[0]));
        errors.Exceptions.ShouldBe(failures);
        errors.Tokens.ShouldAllBe(token => token == cts.Token);
        errors.EventTypes.ShouldAllBe(type => type == typeof(TrackedTestEvent));
        var persisted = state.Rows.ShouldHaveSingleItem();
        persisted.TrackingID.ShouldBe(attempts[0].TrackingID);
        persisted.SubscriberID.ShouldBe(subscriberId);
        persisted.EventType.ShouldBe(eventTypeName);
        if (ack)
            persisted.TrackingID.ShouldBe(invoker.TrackingID);
        sem.CurrentCount.ShouldBe(1);
        invoker.SuccessfulAcks.ShouldBe(ack ? 1 : 0);
        logger.Messages.Count(message => message.Contains("store failure")).ShouldBe(2);
        logger.Messages.Count(message => message.Contains("exception receiver fault during 'store-event'")).ShouldBe(throwOnError ? 2 : 0);
        logger.Messages.ShouldAllBe(message => message.Contains(subscriberId) && message.Contains(eventTypeName));
    }

    sealed class StoreRetryErrors(bool throwOnError) : SubscriberExceptionReceiver
    {
        public List<int> Counts { get; } = [];
        public List<IEventStorageRecord> Records { get; } = [];
        public List<Exception> Exceptions { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];
        public List<Type> EventTypes { get; } = [];

        public override async Task OnStoreEventRecordError<TEvent>(IEventStorageRecord record, int attemptCount,
                                                                  Exception exception, CancellationToken ct)
        {
            Counts.Add(attemptCount);
            Records.Add(record);
            Exceptions.Add(exception);
            Tokens.Add(ct);
            EventTypes.Add(typeof(TEvent));
            await Task.Yield();
            if (throwOnError)
                throw new InvalidOperationException("store callback failed");
        }
    }

    sealed class StoreRetryLogger : ILogger
    {
        public List<string> Messages { get; } = [];
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                                Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
