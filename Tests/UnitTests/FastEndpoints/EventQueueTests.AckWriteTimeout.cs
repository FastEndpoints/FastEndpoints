using FastEndpoints;
using Grpc.Core;
using Xunit;
using static QueueTesting.QueueTestSupport;

namespace EventQueue;

public partial class EventQueueTests
{
    [Theory]
    [InlineData("timeout")]
    [InlineData("expiry")]
    [InlineData("connection")]
    [InlineData("slow")]
    public Task delivery_ack_write_deadline_observes_cleanup_and_preserves_ack_window(string mode)
        => mode switch
        {
            "expiry" => VerifyWriteDeadline<DeliveryAckWriteExpiryEvent>(mode),
            "connection" => VerifyWriteDeadline<DeliveryAckWriteCancelEvent>(mode),
            "slow" => VerifyWriteDeadline<DeliveryAckSlowWriteEvent>(mode),
            _ => VerifyWriteDeadline<DeliveryAckWriteDeadlineEvent>(mode)
        };

    static async Task VerifyWriteDeadline<TEvent>(string mode) where TEvent : class, IEvent, new()
    {
        await using var session = DeliveryAckSession<TEvent>.Create();
        session.HubState.AckTimeout = TimeSpan.FromMilliseconds(mode == "expiry" ? 30000 : mode == "slow" ? 1000 : 200);
        const string subscriberId = "blocked-write";
        var id = Guid.NewGuid();
        session.HubState.Rows.Add(new()
        {
            SubscriberID = subscriberId, TrackingID = id, EventType = typeof(TEvent).FullName!,
            Event = new TEvent(), ExpireOn = DateTime.UtcNow.AddMilliseconds(mode == "expiry" ? 500 : 10000)
        });
        var reader = new ObservedAckReader();
        reader.Send(new() { SubscriberID = subscriberId });
        var writer = new GatedDeliveryWriter<TEvent>();
        using var connection = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var call = session.Hub.OnDeliveryAck(session.Hub, reader, writer, CreateServerCallContext(connection.Token));
        try
        {
            await writer.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            session.ConnectionCount(subscriberId).ShouldBe(1);
            if (mode == "slow")
            {
                await Task.Delay(600);
                writer.Release.TrySetResult();
                await reader.AckReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
                await Task.Delay(600);
                call.IsCompleted.ShouldBeFalse();
                reader.Send(new() { TrackingID = id });
                await WaitUntil(() => session.HubState.Rows.Single().IsComplete);
                connection.Cancel();
                await call.WaitAsync(TimeSpan.FromSeconds(3));
            }
            else
            {
                if (mode == "connection")
                    connection.Cancel();
                await writer.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(3));
                call.IsCompleted.ShouldBeFalse();
                session.ConnectionCount(subscriberId).ShouldBe(1);
                writer.FinishCleanup.TrySetResult();
                if (mode is "timeout" or "expiry")
                {
                    var error = await Should.ThrowAsync<RpcException>(() => call.WaitAsync(TimeSpan.FromSeconds(3)));
                    error.StatusCode.ShouldBe(StatusCode.DeadlineExceeded);
                }
                else
                    await call.WaitAsync(TimeSpan.FromSeconds(3));
                session.HubState.MarkCalls.ShouldBe(0);
                session.HubState.Rows.Single().IsComplete.ShouldBeFalse();
                reader.AckReadStarted.Task.IsCompleted.ShouldBeFalse();
            }
            writer.Finished.ShouldBeTrue();
            session.ConnectionCount(subscriberId).ShouldBe(0);
        }
        finally
        {
            connection.Cancel();
            writer.FinishCleanup.TrySetResult();
            try { await call.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (RpcException) { }
        }
    }

    [Fact]
    public async Task delivery_ack_app_shutdown_observes_blocked_write_cleanup()
    {
        using var app = new CancellationTokenSource();
        var state = new DeliveryAckHubState();
        state.Rows.Add(new()
        {
            SubscriberID = "app-write", TrackingID = Guid.NewGuid(), EventType = typeof(DeliveryAckWriteDeadlineEvent).FullName!,
            Event = new DeliveryAckWriteDeadlineEvent(), ExpireOn = DateTime.UtcNow.AddMinutes(1)
        });
        var registry = new SubscriberRegistry();
        var writer = new GatedDeliveryWriter<DeliveryAckWriteDeadlineEvent>();
        var ctx = new HubContext(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, null,
                                 typeof(DeliveryAckWriteDeadlineEvent).FullName!, app.Token);
        var call = EventDeliveryAckDispatcher.RunAsync<DeliveryAckWriteDeadlineEvent, DeliveryAckRecord, DeliveryAckHubStorage>(
            new(state), registry, ctx, "app-write", new PendingAckReader(), writer, CancellationToken.None);
        try
        {
            await writer.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            app.Cancel();
            await writer.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(3));
            call.IsCompleted.ShouldBeFalse();
            writer.FinishCleanup.TrySetResult();
            await call.WaitAsync(TimeSpan.FromSeconds(3));
            writer.Finished.ShouldBeTrue();
            state.Rows.Single().IsComplete.ShouldBeFalse();
            state.MarkCalls.ShouldBe(0);
        }
        finally
        {
            app.Cancel();
            writer.FinishCleanup.TrySetResult();
            await call.WaitAsync(TimeSpan.FromSeconds(3));
            registry.Subscribers["app-write"].ConnectionCount.ShouldBe(0);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task delivery_ack_live_grpc_unread_write_releases_ownership(bool expireHead)
        => expireHead ? VerifyUnreadDelivery<DeliveryAckUnreadExpiryEvent>(true) : VerifyUnreadDelivery<DeliveryAckUnreadTimeoutEvent>(false);

    static async Task VerifyUnreadDelivery<TEvent>(bool expireHead) where TEvent : class, IEvent, new()
    {
        await using var host = await LiveDeliveryAckHost<TEvent>.CreateAsync(TimeSpan.FromSeconds(expireHead ? 30 : 1),
            new Grpc.Net.Client.GrpcChannelOptions { MaxReceiveMessageSize = 64 * 1024 * 1024 });
        var state = host.HubState;
        var channel = host.Channel;
        var method = host.Method;
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        const string subscriberId = "unread-grpc";
        var hubType = typeof(EventHub<TEvent, DeliveryAckRecord, DeliveryAckHubStorage>);
        int Connections() => TryGetSubscriberFromHub(hubType, subscriberId, out var subscriber)
                                 ? (int)subscriber!.GetType().GetProperty("ConnectionCount")!.GetValue(subscriber)!
                                 : 0;
        using var stalled = channel.CreateCallInvoker().AsyncDuplexStreamingCall(method, null, new(cancellationToken: lifetime.Token));
        await stalled.RequestStream.WriteAsync(new() { SubscriberID = subscriberId });
        await WaitUntil(() => Connections() == 1);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        lock (state.Sync)
            state.Rows.AddRange([
                new()
                {
                    SubscriberID = subscriberId, TrackingID = firstId, EventType = typeof(TEvent).FullName!,
                    Event = new TEvent(), ExpireOn = DateTime.UtcNow.AddSeconds(expireHead ? 1 : 60)
                },
                new()
                {
                    SubscriberID = subscriberId, TrackingID = secondId, EventType = typeof(TEvent).FullName!,
                    Event = new TEvent(), ExpireOn = DateTime.UtcNow.AddMinutes(1)
                }
            ]);
        TryGetSubscriberFromHub(hubType, subscriberId, out var active).ShouldBeTrue();
        ((SemaphoreSlim)active!.GetType().GetProperty("Sem")!.GetValue(active)!).Release();
        // Leave the response unread until the server has released exclusive ownership.
        await WaitUntil(() => Connections() == 0);
        state.MarkCalls.ShouldBe(0);
        state.Rows.ShouldAllBe(row => !row.IsComplete);

        state.AckTimeout = TimeSpan.FromSeconds(5);
        using var replay = channel.CreateCallInvoker().AsyncDuplexStreamingCall(method, null, new(cancellationToken: lifetime.Token));
        await replay.RequestStream.WriteAsync(new() { SubscriberID = subscriberId });
        (await replay.ResponseStream.MoveNext(lifetime.Token)).ShouldBeTrue();
        replay.ResponseStream.Current.TrackingID.ShouldBe(expireHead ? secondId : firstId);
        await replay.RequestStream.WriteAsync(new() { TrackingID = replay.ResponseStream.Current.TrackingID });
        if (!expireHead)
        {
            (await replay.ResponseStream.MoveNext(lifetime.Token)).ShouldBeTrue();
            replay.ResponseStream.Current.TrackingID.ShouldBe(secondId);
            await replay.RequestStream.WriteAsync(new() { TrackingID = secondId });
        }
        await WaitUntil(() => state.Rows.Single(row => row.TrackingID == secondId).IsComplete);
        state.Rows.Single(row => row.TrackingID == firstId).IsComplete.ShouldBe(!expireHead);
        await replay.RequestStream.CompleteAsync();
    }

    public sealed class DeliveryAckWriteDeadlineEvent : IEvent;
    public sealed class DeliveryAckWriteExpiryEvent : IEvent;
    public sealed class DeliveryAckWriteCancelEvent : IEvent;
    public sealed class DeliveryAckSlowWriteEvent : IEvent;
    public sealed class DeliveryAckUnreadTimeoutEvent : IEvent
    {
        public byte[] Data { get; set; } = new byte[32 * 1024 * 1024];
    }
    public sealed class DeliveryAckUnreadExpiryEvent : IEvent
    {
        public byte[] Data { get; set; } = new byte[32 * 1024 * 1024];
    }

    sealed class GatedDeliveryWriter<TEvent> : IServerStreamWriter<EventDelivery<TEvent>> where TEvent : class, IEvent
    {
        public WriteOptions? WriteOptions { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FinishCleanup { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Finished;
        public Task WriteAsync(EventDelivery<TEvent> message) => WriteAsync(message, CancellationToken.None);
        public async Task WriteAsync(EventDelivery<TEvent> message, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Release.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Canceled.TrySetResult();
                await FinishCleanup.Task;
                throw;
            }
            finally
            {
                Finished = true;
            }
        }
    }
}
