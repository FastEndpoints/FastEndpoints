using FastEndpoints;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace EventQueue;

public partial class EventQueueTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task receiver_supervision_isolates_throwing_receive_error_callbacks(bool deliveryAck)
    {
        using var cts = new CancellationTokenSource();
        using var sem = new SemaphoreSlim(0);
        var errors = new SupervisionErrors { ThrowOnReceive = true };
        var invoker = new SupervisionInvoker(cts, [SupervisionStep.Fail(), SupervisionStep.Fail(), SupervisionStep.Stop()]);

        await RunSupervisedReceiver(deliveryAck, invoker, errors, sem, cts.Token).WaitAsync(TimeSpan.FromSeconds(3));

        errors.Counts.ShouldBe([1, 2]);
        errors.Exceptions.ShouldAllBe(ex => ex is IOException);
        invoker.CreationAttempts.ShouldBe(3);
        invoker.Disposals.ShouldBe(3);
        invoker.ReplacedBeforeDisposal.ShouldBeFalse();
        sem.CurrentCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task receiver_supervision_cancellation_during_reconnect_delay_does_not_replace_call(bool deliveryAck)
    {
        using var cts = new CancellationTokenSource();
        using var sem = new SemaphoreSlim(0);
        var errors = new SupervisionErrors();
        var invoker = new SupervisionInvoker(cts, [SupervisionStep.Fail(), SupervisionStep.Stop()]);
        var receiver = RunSupervisedReceiver(deliveryAck, invoker, errors, sem, cts.Token, TimeSpan.FromMinutes(1));

        // The scripted failure is synchronous, so RunAsync has entered its reconnect delay before returning.
        errors.Counts.ShouldBe([1]);
        receiver.IsCompleted.ShouldBeFalse();
        invoker.Disposals.ShouldBe(1);
        cts.Cancel();
        await receiver.WaitAsync(TimeSpan.FromSeconds(3));

        invoker.CreationAttempts.ShouldBe(1);
        invoker.Disposals.ShouldBe(1);
        errors.Counts.ShouldBe([1]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task receiver_supervision_disposes_session_before_creating_replacement(bool deliveryAck, bool gracefulEnd)
    {
        using var cts = new CancellationTokenSource();
        using var sem = new SemaphoreSlim(0);
        var errors = new SupervisionErrors();
        var invoker = new SupervisionInvoker(cts, [gracefulEnd ? SupervisionStep.End() : SupervisionStep.Fail(), SupervisionStep.Stop()]);

        await RunSupervisedReceiver(deliveryAck, invoker, errors, sem, cts.Token).WaitAsync(TimeSpan.FromSeconds(3));

        invoker.CreationAttempts.ShouldBe(2);
        invoker.Disposals.ShouldBe(2);
        invoker.ReplacedBeforeDisposal.ShouldBeFalse();
        errors.Counts.ShouldBe(gracefulEnd ? Array.Empty<int>() : [1]);
    }

    [Fact]
    public async Task ordinary_receiver_initial_call_creation_failure_propagates_without_receive_callback()
    {
        using var cts = new CancellationTokenSource();
        using var sem = new SemaphoreSlim(0);
        var errors = new SupervisionErrors();
        var invoker = new SupervisionInvoker(cts, [SupervisionStep.CreationFailure()]);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => RunSupervisedReceiver(false, invoker, errors, sem, cts.Token));

        exception.ShouldBeSameAs(invoker.CreationError);
        errors.Counts.ShouldBeEmpty();
        invoker.CreationAttempts.ShouldBe(1);
        invoker.Disposals.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ordinary_receiver_replacement_call_creation_failure_is_terminal_without_receive_callback(bool receiveFailure)
    {
        using var cts = new CancellationTokenSource();
        using var sem = new SemaphoreSlim(0);
        var errors = new SupervisionErrors();
        var invoker = new SupervisionInvoker(cts, [receiveFailure ? SupervisionStep.Fail() : SupervisionStep.End(), SupervisionStep.CreationFailure()]);

        await RunSupervisedReceiver(false, invoker, errors, sem, cts.Token).WaitAsync(TimeSpan.FromSeconds(3));

        errors.Counts.ShouldBe(receiveFailure ? [1] : Array.Empty<int>());
        errors.Exceptions.ShouldNotContain(invoker.CreationError);
        invoker.CreationAttempts.ShouldBe(2);
        invoker.Disposals.ShouldBe(1);
        invoker.ReplacedBeforeDisposal.ShouldBeFalse();
        cts.IsCancellationRequested.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ack_receiver_call_creation_failure_reports_and_reconnects(bool replacement)
    {
        using var cts = new CancellationTokenSource();
        using var sem = new SemaphoreSlim(0);
        var errors = new SupervisionErrors();
        SupervisionStep[] steps = replacement
                                      ? [SupervisionStep.End(), SupervisionStep.CreationFailure(), SupervisionStep.Stop()]
                                      : [SupervisionStep.CreationFailure(), SupervisionStep.Stop()];
        var invoker = new SupervisionInvoker(cts, steps);

        await RunSupervisedReceiver(true, invoker, errors, sem, cts.Token).WaitAsync(TimeSpan.FromSeconds(3));

        errors.Counts.ShouldBe([1]);
        errors.Exceptions.Single().ShouldBeSameAs(invoker.CreationError);
        invoker.CreationAttempts.ShouldBe(steps.Length);
        invoker.Disposals.ShouldBe(steps.Length - 1);
        invoker.ReplacedBeforeDisposal.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task receiver_supervision_resets_receive_error_count_after_successful_delivery(bool deliveryAck)
    {
        using var cts = new CancellationTokenSource();
        using var sem = new SemaphoreSlim(0);
        var errors = new SupervisionErrors();
        var invoker = new SupervisionInvoker(cts,
                                            [SupervisionStep.Fail(), SupervisionStep.Fail(), SupervisionStep.Deliver(), SupervisionStep.Stop()]);
        var storage = new TestEventSubscriberStorage();
        var ackState = new DeliveryAckSubscriberState();

        await RunSupervisedReceiver(deliveryAck, invoker, errors, sem, cts.Token, storage: storage, ackState: ackState)
            .WaitAsync(TimeSpan.FromSeconds(3));

        errors.Counts.ShouldBe([1, 2, 1]);
        sem.CurrentCount.ShouldBe(1);
        if (deliveryAck)
        {
            ackState.StoreCalls.ShouldBe(1);
            ackState.Rows.Single().TrackingID.ShouldBe(invoker.TrackingID);
            invoker.SuccessfulAcks.ShouldBe(1);
        }
        else
        {
            var rows = await storage.GetNextBatchAsync(new() { Match = _ => true, Limit = 10, CancellationToken = CancellationToken.None });
            ((TrackedTestEvent)rows.Single().Event).Name.ShouldBe("supervised");
        }
    }

    [Fact]
    public async Task ack_receiver_failed_ack_does_not_reset_receive_error_count_but_successful_ack_does()
    {
        using var cts = new CancellationTokenSource();
        using var sem = new SemaphoreSlim(0);
        var errors = new SupervisionErrors();
        var ackState = new DeliveryAckSubscriberState();
        var invoker = new SupervisionInvoker(cts,
                                            [SupervisionStep.Fail(), SupervisionStep.Fail(), SupervisionStep.Deliver(failAck: true),
                                             SupervisionStep.Deliver(), SupervisionStep.Stop()]);

        await RunSupervisedReceiver(true, invoker, errors, sem, cts.Token, ackState: ackState).WaitAsync(TimeSpan.FromSeconds(3));

        errors.Counts.ShouldBe([1, 2, 3, 1]);
        errors.Exceptions[2].ShouldBeSameAs(invoker.AckError);
        ackState.StoreCalls.ShouldBe(1);
        ackState.DuplicateCount.ShouldBe(1);
        invoker.SuccessfulAcks.ShouldBe(1);
        sem.CurrentCount.ShouldBe(2);
        invoker.Disposals.ShouldBe(5);
        invoker.ReplacedBeforeDisposal.ShouldBeFalse();
    }

    static Task RunSupervisedReceiver(bool deliveryAck,
                                      SupervisionInvoker invoker,
                                      SupervisionErrors errors,
                                      SemaphoreSlim sem,
                                      CancellationToken ct,
                                      TimeSpan? retryDelay = null,
                                      TestEventSubscriberStorage? storage = null,
                                      DeliveryAckSubscriberState? ackState = null)
    {
        var opts = new CallOptions(cancellationToken: ct);
        var delay = retryDelay ?? TimeSpan.Zero;
        if (!deliveryAck)
            return EventReceiverWorker.RunAsync<TrackedTestEvent, TestEventRecord, TestEventSubscriberStorage>(
                storage ?? new(), SubscriberStorageBehavior.Durable, sem, opts, invoker, CreateSubscriptionMethod<TrackedTestEvent>(),
                "supervised-subscriber", typeof(TrackedTestEvent).FullName!, TimeSpan.FromMinutes(1), NullLogger.Instance, errors, delay);

        var method = new Method<EventDeliveryAck, EventDelivery<TrackedTestEvent>>(
            MethodType.DuplexStreaming, typeof(TrackedTestEvent).FullName!, "sub-ack",
            new MessagePackMarshaller<EventDeliveryAck>(), new MessagePackMarshaller<EventDelivery<TrackedTestEvent>>());
        return EventDeliveryAckReceiver.RunAsync<TrackedTestEvent, DeliveryAckRecord, DeliveryAckSubscriberStorage>(
            new(ackState ?? new()), SubscriberStorageBehavior.Durable, sem, opts, invoker, method,
            "supervised-subscriber", typeof(TrackedTestEvent).FullName!, TimeSpan.FromMinutes(1), NullLogger.Instance, errors, delay);
    }

    sealed class SupervisionErrors : SubscriberExceptionReceiver
    {
        public List<int> Counts { get; } = [];
        public List<Exception> Exceptions { get; } = [];
        public bool ThrowOnReceive { get; init; }

        public override Task OnEventReceiveError<TEvent>(string subscriberID, int attemptCount, Exception exception, CancellationToken ct)
        {
            Counts.Add(attemptCount);
            Exceptions.Add(exception);
            if (ThrowOnReceive)
                throw new InvalidOperationException("receive callback failed");

            return Task.CompletedTask;
        }
    }

    sealed record SupervisionStep(bool ThrowOnCreate = false, bool HasEvent = false, bool FailAck = false,
                                  bool FailRead = false, bool Cancel = false)
    {
        public static SupervisionStep Fail() => new(FailRead: true);
        public static SupervisionStep End() => new();
        public static SupervisionStep Stop() => new(Cancel: true);
        public static SupervisionStep CreationFailure() => new(ThrowOnCreate: true);
        public static SupervisionStep Deliver(bool failAck = false) => new(HasEvent: true, FailAck: failAck, FailRead: true);
    }

    sealed class SupervisionInvoker(CancellationTokenSource cts, SupervisionStep[] steps) : CallInvoker
    {
        bool _callOpen;
        public int CreationAttempts { get; private set; }
        public int Disposals { get; private set; }
        public int SuccessfulAcks { get; private set; }
        public bool ReplacedBeforeDisposal { get; private set; }
        public Guid TrackingID { get; } = Guid.NewGuid();
        public Exception CreationError { get; } = new InvalidOperationException("call creation failed");
        public Exception AckError { get; } = new IOException("ACK write failed");

        SupervisionStep Create()
        {
            ReplacedBeforeDisposal |= _callOpen;
            var index = CreationAttempts++;
            if (index >= steps.Length)
                throw new InvalidOperationException("Unexpected replacement call.");
            var step = steps[index];
            if (step.ThrowOnCreate)
                throw CreationError;
            _callOpen = true;

            return step;
        }

        void DisposeCall()
        {
            Disposals++;
            _callOpen = false;
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            var step = Create();
            var reader = new SupervisionReader<TrackedTestEvent>(step, new() { Name = "supervised" }, cts);
            return (AsyncServerStreamingCall<TResponse>)(object)new AsyncServerStreamingCall<TrackedTestEvent>(
                reader, Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new(), DisposeCall);
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options)
        {
            var step = Create();
            var delivery = new EventDelivery<TrackedTestEvent>
            {
                TrackingID = TrackingID, Event = new() { Name = "supervised" }, ReplayUntil = DateTime.UtcNow.AddMinutes(5)
            };
            var reader = new SupervisionReader<EventDelivery<TrackedTestEvent>>(step, delivery, cts);
            return (AsyncDuplexStreamingCall<TRequest, TResponse>)(object)new AsyncDuplexStreamingCall<EventDeliveryAck, EventDelivery<TrackedTestEvent>>(
                new SupervisionAckWriter(this, step), reader, Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess, () => new(), DisposeCall);
        }

        sealed class SupervisionAckWriter(SupervisionInvoker owner, SupervisionStep step) : IClientStreamWriter<EventDeliveryAck>
        {
            public WriteOptions? WriteOptions { get; set; }
            public Task CompleteAsync() => Task.CompletedTask;
            public Task WriteAsync(EventDeliveryAck message)
            {
                if (message.TrackingID != Guid.Empty)
                {
                    if (step.FailAck)
                        throw owner.AckError;
                    owner.SuccessfulAcks++;
                }

                return Task.CompletedTask;
            }
        }

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
    }

    sealed class SupervisionReader<T>(SupervisionStep step, T message, CancellationTokenSource cts) : IAsyncStreamReader<T>
    {
        bool _delivered;
        public T Current => message;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (step.Cancel)
            {
                cts.Cancel();
                return Task.FromResult(false);
            }
            if (step.HasEvent && !_delivered)
            {
                _delivered = true;
                return Task.FromResult(true);
            }
            if (step.FailRead)
                throw new IOException("stream read failed");

            return Task.FromResult(false);
        }
    }
}
