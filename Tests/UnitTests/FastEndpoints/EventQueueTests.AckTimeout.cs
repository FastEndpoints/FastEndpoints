using FastEndpoints;
using Grpc.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using QueueTesting;
using Xunit;
using static QueueTesting.QueueTestSupport;

namespace EventQueue;

public partial class EventQueueTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task delivery_ack_timeout_releases_connection_and_replays_pending_backlog(bool expireHead)
        => expireHead ? VerifyAckTimeout<DeliveryAckExpiryEvent>(true) : VerifyAckTimeout<DeliveryAckTimeoutEvent>(false);

    static async Task VerifyAckTimeout<TEvent>(bool expireHead) where TEvent : class, IEvent, new()
    {
        await using var session = DeliveryAckSession<TEvent>.Create();
        session.HubState.AckTimeout = expireHead ? TimeSpan.FromSeconds(30) : TimeSpan.FromMilliseconds(200);
        const string subscriberId = "timeout-sub";
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        session.HubState.Rows.AddRange([
            new()
            {
                SubscriberID = subscriberId, TrackingID = firstId, EventType = typeof(TEvent).FullName!,
                Event = new TEvent(), ExpireOn = DateTime.UtcNow.AddMilliseconds(expireHead ? 500 : 10000)
            },
            new()
            {
                SubscriberID = subscriberId, TrackingID = secondId, EventType = typeof(TEvent).FullName!,
                Event = new TEvent(), ExpireOn = DateTime.UtcNow.AddMinutes(1)
            }
        ]);
        var reader = new ObservedAckReader();
        reader.Send(new() { SubscriberID = subscriberId });
        var writer = new TestServerStreamWriter<EventDelivery<TEvent>>();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var call = session.Hub.OnDeliveryAck(session.Hub, reader, writer, CreateServerCallContext(lifetime.Token));
        await reader.AckReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        session.ConnectionCount(subscriberId).ShouldBe(1);
        var error = await Should.ThrowAsync<RpcException>(() => call.WaitAsync(TimeSpan.FromSeconds(3)));
        error.StatusCode.ShouldBe(StatusCode.DeadlineExceeded);
        reader.ActiveReads.ShouldBe(0);
        reader.CanceledReads.ShouldBe(1);
        session.ConnectionCount(subscriberId).ShouldBe(0);
        writer.Responses.Single().TrackingID.ShouldBe(firstId);
        session.HubState.MarkCalls.ShouldBe(0);
        session.HubState.Rows.ShouldAllBe(row => !row.IsComplete);

        session.HubState.AckTimeout = TimeSpan.FromSeconds(5);
        var replay = new PendingAckReader();
        replay.Send(new() { SubscriberID = subscriberId });
        var replayWriter = new TestServerStreamWriter<EventDelivery<TEvent>>();
        var replayCall = session.Hub.OnDeliveryAck(session.Hub, replay, replayWriter, CreateServerCallContext(lifetime.Token));
        try
        {
            await WaitUntil(() => replayWriter.Responses.Count == 1);
            replayWriter.Responses[0].TrackingID.ShouldBe(expireHead ? secondId : firstId);
            replay.Send(new() { TrackingID = replayWriter.Responses[0].TrackingID });
            if (!expireHead)
            {
                await WaitUntil(() => replayWriter.Responses.Count == 2);
                replayWriter.Responses[1].TrackingID.ShouldBe(secondId);
                replay.Send(new() { TrackingID = secondId });
            }
            await WaitUntil(() => session.HubState.Rows.Single(row => row.TrackingID == secondId).IsComplete);
            session.HubState.Rows.Single(row => row.TrackingID == firstId).IsComplete.ShouldBe(!expireHead);
        }
        finally
        {
            lifetime.Cancel();
            await replayCall.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task delivery_ack_cancellation_observes_pending_read_and_keeps_row_pending(bool cancelApp)
    {
        using var app = new CancellationTokenSource();
        using var connection = new CancellationTokenSource();
        var state = new DeliveryAckHubState();
        state.Rows.Add(new()
        {
            SubscriberID = "cancel-sub", TrackingID = Guid.NewGuid(), EventType = typeof(DeliveryAckTimeoutEvent).FullName!,
            Event = new DeliveryAckTimeoutEvent(), ExpireOn = DateTime.UtcNow.AddMinutes(1)
        });
        var registry = new SubscriberRegistry();
        var reader = new ObservedAckReader(startWithRegistration: false);
        var ctx = new HubContext(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, null,
                                 typeof(DeliveryAckTimeoutEvent).FullName!, app.Token);
        var call = EventDeliveryAckDispatcher.RunAsync<DeliveryAckTimeoutEvent, DeliveryAckRecord, DeliveryAckHubStorage>(
            new(state), registry, ctx, "cancel-sub", reader,
            new TestServerStreamWriter<EventDelivery<DeliveryAckTimeoutEvent>>(), connection.Token);
        await reader.AckReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        (cancelApp ? app : connection).Cancel();
        await call.WaitAsync(TimeSpan.FromSeconds(3));
        reader.ActiveReads.ShouldBe(0);
        reader.CanceledReads.ShouldBe(1);
        state.MarkCalls.ShouldBe(0);
        state.Rows.Single().IsComplete.ShouldBeFalse();
        registry.Subscribers["cancel-sub"].ConnectionCount.ShouldBe(0);
    }

    [Fact]
    public async Task delivery_ack_timeout_during_slow_inbox_store_recovers_with_one_handler_execution()
    {
        await using var session = DeliveryAckSession<DeliveryAckSlowStoreEvent>.Create();
        session.HubState.AckTimeout = TimeSpan.FromMilliseconds(500);
        session.SubState.GateStores = true;
        await session.Connect("slow-store");
        await session.Hub.BroadcastEventTaskForTesting(new());
        await WaitUntil(() => session.SubState.StoresWaiting == 1);
        await WaitUntil(() => session.ConnectionCount("slow-store") == 0);
        session.HubState.MarkCalls.ShouldBe(0);
        session.SubState.Rows.ShouldBeEmpty();
        session.HubState.AckTimeout = TimeSpan.FromSeconds(5);
        session.SubState.ReleaseStores.TrySetResult();
        await WaitUntil(() => session.HubState.Rows.Single().IsComplete &&
                              DeliveryAckCountingHandler<DeliveryAckSlowStoreEvent>.Runs == 1);
        session.SubState.Rows.Count.ShouldBe(1);
        session.SubState.StoreCalls.ShouldBe(1);
        session.SubState.DuplicateCount.ShouldBe(1);
    }

    [Fact]
    public async Task delivery_ack_timeout_after_inbox_commit_replays_as_duplicate_and_handles_once()
    {
        await using var session = DeliveryAckSession<DeliveryAckTimeoutCommitEvent>.Create();
        session.HubState.AckTimeout = TimeSpan.FromMilliseconds(500);
        session.Bridge.GateAcks = true;
        await session.Connect("commit-timeout");
        await session.Hub.BroadcastEventTaskForTesting(new());
        await session.Bridge.AckWaiting.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await WaitUntil(() => session.SubState.Rows.Single().IsComplete);
        await WaitUntil(() => session.ConnectionCount("commit-timeout") == 0);
        session.HubState.Rows.Single().IsComplete.ShouldBeFalse();
        session.HubState.AckTimeout = TimeSpan.FromSeconds(5);
        session.Bridge.GateAcks = false;
        session.Bridge.ReleaseAcks.TrySetResult();
        await WaitUntil(() => session.HubState.Rows.Single().IsComplete);
        session.SubState.Rows.Count.ShouldBe(1);
        session.SubState.StoreCalls.ShouldBe(1);
        session.SubState.DuplicateCount.ShouldBe(1);
        DeliveryAckCountingHandler<DeliveryAckTimeoutCommitEvent>.Runs.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4294967295)]
    public async Task delivery_ack_timeout_requires_a_positive_supported_timer(long milliseconds)
    {
        var state = new DeliveryAckHubState { AckTimeout = TimeSpan.FromMilliseconds(milliseconds) };
        var registry = new SubscriberRegistry();
        var ctx = new HubContext(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, null,
                                 typeof(DeliveryAckTimeoutEvent).FullName!, CancellationToken.None);

        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            EventDeliveryAckDispatcher.RunAsync<DeliveryAckTimeoutEvent, DeliveryAckRecord, DeliveryAckHubStorage>(
                new(state), registry, ctx, "invalid-timeout", new PendingAckReader(),
                new TestServerStreamWriter<EventDelivery<DeliveryAckTimeoutEvent>>(), CancellationToken.None));

        registry.Subscribers["invalid-timeout"].ConnectionCount.ShouldBe(0);
        state.MarkCalls.ShouldBe(0);
    }

    sealed class LiveDeliveryAckHost<TEvent>(Microsoft.AspNetCore.Builder.WebApplication app,
                                           DeliveryAckHubState hubState,
                                           Grpc.Net.Client.GrpcChannel channel) : IAsyncDisposable
        where TEvent : class, IEvent, new()
    {
        public DeliveryAckHubState HubState { get; } = hubState;
        public IServiceProvider Services => app.Services;
        public Grpc.Net.Client.GrpcChannel Channel { get; } = channel;
        public Method<EventDeliveryAck, EventDelivery<TEvent>> Method { get; } = new(
            MethodType.DuplexStreaming, typeof(TEvent).FullName + "/sub-ack", "",
            new MessagePackMarshaller<EventDeliveryAck>(), new MessagePackMarshaller<EventDelivery<TEvent>>());

        public static async Task<LiveDeliveryAckHost<TEvent>> CreateAsync(TimeSpan ackTimeout,
                                                                         Grpc.Net.Client.GrpcChannelOptions? channelOptions = null)
        {
            var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options => options.Listen(System.Net.IPAddress.Loopback, 0,
                listen => listen.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2));
            var state = new DeliveryAckHubState { AckTimeout = ackTimeout };
            builder.Services.AddSingleton(state);
            builder.Services.AddHandlerServer();
            var app = builder.Build();
            Grpc.Net.Client.GrpcChannel? channel = null;
            try
            {
                app.MapHandlers<DeliveryAckRecord, DeliveryAckHubStorage>(h => h.RegisterEventHub<TEvent>());
                await app.StartAsync();
                var address = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
                                 .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!.Addresses.Single();
                channel = channelOptions is null
                              ? Grpc.Net.Client.GrpcChannel.ForAddress(address)
                              : Grpc.Net.Client.GrpcChannel.ForAddress(address, channelOptions);
                return new(app, state, channel);
            }
            catch
            {
                try
                {
                    channel?.Dispose();
                }
                finally
                {
                    await StopAndDisposeAsync(app);
                }
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                Channel.Dispose();
            }
            finally
            {
                await StopAndDisposeAsync(app);
            }
        }

        static async Task StopAndDisposeAsync(Microsoft.AspNetCore.Builder.WebApplication app)
        {
            try
            {
                await app.StopAsync();
            }
            finally
            {
                await app.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task delivery_ack_live_grpc_call_without_ack_times_out_and_reconnects()
    {
        await using var host = await LiveDeliveryAckHost<DeliveryAckTransportEvent>.CreateAsync(TimeSpan.FromSeconds(1));
        var state = host.HubState;
        var channel = host.Channel;
        var method = host.Method;
        var hub = new EventHub<DeliveryAckTransportEvent, DeliveryAckRecord, DeliveryAckHubStorage>(host.Services);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var stalled = channel.CreateCallInvoker().AsyncDuplexStreamingCall(method, null, new(cancellationToken: lifetime.Token));
        await stalled.RequestStream.WriteAsync(new() { SubscriberID = "grpc-timeout" });
        await WaitUntil(() => TryGetSubscriberFromHub(typeof(EventHub<DeliveryAckTransportEvent, DeliveryAckRecord, DeliveryAckHubStorage>),
                                                     "grpc-timeout", out _));
        await hub.BroadcastEventTaskForTesting(new());
        await hub.BroadcastEventTaskForTesting(new());
        (await stalled.ResponseStream.MoveNext(lifetime.Token)).ShouldBeTrue();
        var firstId = stalled.ResponseStream.Current.TrackingID;
        var error = await Should.ThrowAsync<RpcException>(() => stalled.ResponseStream.MoveNext(lifetime.Token));
        error.StatusCode.ShouldBe(StatusCode.DeadlineExceeded);
        state.MarkCalls.ShouldBe(0);
        state.Rows.ShouldAllBe(row => !row.IsComplete);

        state.AckTimeout = TimeSpan.FromSeconds(5);
        using var replay = channel.CreateCallInvoker().AsyncDuplexStreamingCall(method, null, new(cancellationToken: lifetime.Token));
        await replay.RequestStream.WriteAsync(new() { SubscriberID = "grpc-timeout" });
        (await replay.ResponseStream.MoveNext(lifetime.Token)).ShouldBeTrue();
        replay.ResponseStream.Current.TrackingID.ShouldBe(firstId);
        await replay.RequestStream.WriteAsync(new() { TrackingID = firstId });
        (await replay.ResponseStream.MoveNext(lifetime.Token)).ShouldBeTrue();
        replay.ResponseStream.Current.TrackingID.ShouldNotBe(firstId);
        await replay.RequestStream.WriteAsync(new() { TrackingID = replay.ResponseStream.Current.TrackingID });
        await WaitUntil(() => state.Rows.All(row => row.IsComplete));
        await replay.RequestStream.CompleteAsync();
    }

    public sealed class DeliveryAckSlowStoreEvent : IEvent;

    public sealed class DeliveryAckTimeoutCommitEvent : IEvent;
    public sealed class DeliveryAckTransportEvent : IEvent;

    public sealed class DeliveryAckExpiryEvent : IEvent;
    public sealed class DeliveryAckTimeoutEvent : IEvent;

    sealed class ObservedAckReader(bool startWithRegistration = true) : IAsyncStreamReader<EventDeliveryAck>
    {
        readonly PendingAckReader _reader = new();
        int _reads;
        public int ActiveReads;
        public int CanceledReads;
        public TaskCompletionSource AckReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public EventDeliveryAck Current => _reader.Current;
        public void Send(EventDeliveryAck ack) => _reader.Send(ack);

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref ActiveReads);
            if (++_reads > (startWithRegistration ? 1 : 0))
                AckReadStarted.TrySetResult();
            try
            {
                return await _reader.MoveNext(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref CanceledReads);
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref ActiveReads);
            }
        }
    }
}
