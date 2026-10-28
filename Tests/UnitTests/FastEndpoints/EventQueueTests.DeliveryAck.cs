using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using FastEndpoints;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using QueueTesting;
using Xunit;
using static QueueTesting.QueueTestSupport;

namespace EventQueue;

// each scenario uses its own event type. EventHub<,,> and the ack subscriber keep static storage per closed generic.
public partial class EventQueueTests
{
    [Fact]
    public async Task delivery_ack_happy_path_stores_hub_tracking_id_and_handles_once()
    {
        await using var session = DeliveryAckSession<DeliveryAckHappyEvent>.Create();
        await session.Connect("happy-sub");

        await session.Hub.BroadcastEventTaskForTesting(new() { EventID = 11 });

        await WaitUntil(() => session.HubState.Rows.Any(r => r.IsComplete) && DeliveryAckCountingHandler<DeliveryAckHappyEvent>.Runs == 1);

        var hubRow = session.HubState.Rows.Single();
        var subRow = session.SubState.Rows.Single();
        hubRow.TrackingID.ShouldNotBe(Guid.Empty);
        subRow.TrackingID.ShouldBe(hubRow.TrackingID);
        ((DeliveryAckHappyEvent)subRow.Event).EventID.ShouldBe(11);
        session.SubState.DuplicateCount.ShouldBe(0);
        session.Bridge.DuplexCalls.ShouldBe(1);
        session.Bridge.ServerStreamingCalls.ShouldBe(0);
        session.Bridge.FullNames.ShouldAllBe(call => call.FullName == session.Bridge.ExpectedFullName && call.Type == MethodType.DuplexStreaming);
    }

    [Fact]
    public Task delivery_ack_drop_after_store_redelivers_as_duplicate_and_handles_once()
        => VerifyDroppedAcks<DeliveryAckDropAfterStoreEvent>(1);

    [Fact]
    public Task delivery_ack_repeated_drops_redeliver_as_duplicates_and_handle_once()
        => VerifyDroppedAcks<DeliveryAckRepeatedDropEvent>(3);

    [Fact]
    public async Task delivery_ack_ambiguous_commit_wakes_executor_and_handles_once()
    {
        await using var session = DeliveryAckSession<DeliveryAckAmbiguousCommitEvent>.Create();
        DeliveryAckRecord? committed = null;
        session.SubState.AfterStore = record =>
        {
            committed = DeliveryAckRecord.Clone(record);

            throw new IOException("Committed insert, response lost");
        };
        await session.Connect("ambiguous-commit", executionExpiry: TimeSpan.FromSeconds(3));
        await session.SubState.EmptyFetch.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await session.Hub.BroadcastEventTaskForTesting(new());

        await WaitUntil(() => session.HubState.Rows.Single().IsComplete);
        session.SubState.DuplicateCount.ShouldBe(1);
        await WaitUntil(() => session.SubState.Rows.Single().IsComplete, timeoutMs: 1500);
        session.SubState.StoreCalls.ShouldBe(1);
        session.SubState.Rows.Count.ShouldBe(1);
        committed.ShouldNotBeNull();
        session.SubState.Rows.Single().ExpireOn.ShouldBe(committed.ExpireOn);
        session.SubState.Rows.Single().RetainUntil.ShouldBe(committed.RetainUntil);
        session.SubState.Rows.Single().TrackingID.ShouldBe(session.HubState.Rows.Single().TrackingID);
        DeliveryAckCountingHandler<DeliveryAckAmbiguousCommitEvent>.Runs.ShouldBe(1);
    }

    public sealed class DeliveryAckAmbiguousCommitEvent : IEvent;

    [Fact]
    public async Task delivery_ack_active_duplicates_handle_once()
    {
        await using var session = DeliveryAckSession<DeliveryAckActiveDuplicateEvent>.Create();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DeliveryAckCountingHandler<DeliveryAckActiveDuplicateEvent>.OnHandle = async ct =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(ct);
        };
        session.Bridge.DropAckCalls = 3;
        await session.Connect("active-duplicate");
        await session.SubState.EmptyFetch.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await session.Hub.BroadcastEventTaskForTesting(new());
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await WaitUntil(() => session.HubState.Rows.Single().IsComplete);
        session.SubState.DuplicateCount.ShouldBe(3);
        session.SubState.Rows.Single().IsComplete.ShouldBeFalse();
        DeliveryAckCountingHandler<DeliveryAckActiveDuplicateEvent>.Runs.ShouldBe(1);

        release.TrySetResult();
        await WaitUntil(() => session.SubState.Rows.Single().IsComplete);
        session.SubState.StoreCalls.ShouldBe(1);
        session.SubState.Rows.Count.ShouldBe(1);
        DeliveryAckCountingHandler<DeliveryAckActiveDuplicateEvent>.Runs.ShouldBe(1);
    }

    public sealed class DeliveryAckActiveDuplicateEvent : IEvent;

    public sealed class DeliveryAckRepeatedDropEvent : IEvent;

    static async Task VerifyDroppedAcks<TEvent>(int droppedAckCalls) where TEvent : class, IEvent, new()
    {
        await using var session = DeliveryAckSession<TEvent>.Create();
        session.Bridge.DropAckCalls = droppedAckCalls;
        DeliveryAckRecord? committed = null;
        session.SubState.AfterStore = record => committed = DeliveryAckRecord.Clone(record);
        await session.Connect("drop-after", executionExpiry: TimeSpan.FromSeconds(3));
        await session.SubState.EmptyFetch.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await session.Hub.BroadcastEventTaskForTesting(new());

        await WaitUntil(() => session.SubState.Rows.Any(row => row.IsComplete), timeoutMs: 1500);
        session.SubState.Rows.Single().ExpireOn.ShouldBeGreaterThan(DateTime.UtcNow);
        await WaitUntil(() => session.HubState.Rows.Single().IsComplete);
        session.SubState.StoreCalls.ShouldBe(1);
        session.SubState.Rows.Count.ShouldBe(1);
        session.SubState.DuplicateCount.ShouldBe(droppedAckCalls);
        committed.ShouldNotBeNull();
        session.SubState.Rows.Single().ExpireOn.ShouldBe(committed.ExpireOn);
        session.SubState.Rows.Single().RetainUntil.ShouldBe(committed.RetainUntil);
        session.SubState.Rows.Single().IsComplete.ShouldBeTrue();
        session.SubState.Rows.Single().TrackingID.ShouldBe(session.HubState.Rows.Single().TrackingID);
        DeliveryAckCountingHandler<TEvent>.Runs.ShouldBe(1);
    }

    [Fact]
    public async Task delivery_ack_blocked_write_does_not_delay_inbox_execution()
    {
        await using var session = DeliveryAckSession<DeliveryAckBlockedWriteEvent>.Create();
        session.Bridge.GateAcks = true;
        await session.Connect("blocked-ack", executionExpiry: TimeSpan.FromSeconds(3));
        await session.SubState.EmptyFetch.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await session.Hub.BroadcastEventTaskForTesting(new() { EventID = 3 });
        await session.Bridge.AckWaiting.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await WaitUntil(() => session.SubState.Rows.Any(row => row.IsComplete), timeoutMs: 1500);
        session.SubState.Rows.Single().ExpireOn.ShouldBeGreaterThan(DateTime.UtcNow);
        session.HubState.Rows.Single().IsComplete.ShouldBeFalse();
        session.SubState.StoreCalls.ShouldBe(1);
        DeliveryAckCountingHandler<DeliveryAckBlockedWriteEvent>.Runs.ShouldBe(1);

        session.Bridge.ReleaseAcks.TrySetResult();
        await WaitUntil(() => session.HubState.Rows.Single().IsComplete);
        session.SubState.DuplicateCount.ShouldBe(0);
        DeliveryAckCountingHandler<DeliveryAckBlockedWriteEvent>.Runs.ShouldBe(1);
    }

    public sealed class DeliveryAckBlockedWriteEvent : IEvent
    {
        public int EventID { get; set; }
    }

    [Fact]
    public async Task delivery_ack_drop_before_store_delivers_once_on_reconnect()
    {
        await using var session = DeliveryAckSession<DeliveryAckDropBeforeStoreEvent>.Create();
        session.Bridge.FailFirstDeliveryRead = true;
        await session.Connect("drop-before");

        await session.Hub.BroadcastEventTaskForTesting(new() { EventID = 3 });

        await WaitUntil(
            () => session.HubState.Rows.Any(r => r.IsComplete) && DeliveryAckCountingHandler<DeliveryAckDropBeforeStoreEvent>.Runs == 1,
            timeoutMs: 15000);

        session.SubState.StoreCalls.ShouldBe(1);
        session.SubState.DuplicateCount.ShouldBe(0);
        session.SubState.Rows.Single().TrackingID.ShouldBe(session.HubState.Rows.Single().TrackingID);
        session.Bridge.DuplexCalls.ShouldBeGreaterThan(1);
        session.Bridge.ServerStreamingCalls.ShouldBe(0);
    }

    [Fact]
    public async Task delivery_ack_tracking_id_mismatch_leaves_the_hub_row_pending()
    {
        await using var session = DeliveryAckSession<DeliveryAckMismatchEvent>.Create();
        var trackingId = Guid.NewGuid();
        session.HubState.Rows.Add(
            new()
            {
                SubscriberID = "mismatch-sub",
                TrackingID = trackingId,
                EventType = typeof(DeliveryAckMismatchEvent).FullName!,
                Event = new DeliveryAckMismatchEvent { EventID = 4 },
                ExpireOn = DateTime.UtcNow.AddHours(1)
            });

        var writer = new TestServerStreamWriter<EventDelivery<DeliveryAckMismatchEvent>>();
        var reader = new ScriptedAckReader(
            new() { SubscriberID = "mismatch-sub" },
            new() { TrackingID = Guid.NewGuid() });

        await session.Hub.OnDeliveryAck(session.Hub, reader, writer, CreateServerCallContext(CancellationToken.None))
                     .WaitAsync(TimeSpan.FromSeconds(3));

        writer.Responses.Count.ShouldBe(1);
        writer.Responses[0].TrackingID.ShouldBe(trackingId);
        writer.Responses[0].Event.EventID.ShouldBe(4);
        session.HubState.MarkCalls.ShouldBe(0);
        session.HubState.Rows.Single().IsComplete.ShouldBeFalse();
    }

    [Fact]
    public async Task delivery_ack_unreadable_head_row_is_completed_and_the_next_event_is_delivered()
    {
        var state = new DeliveryAckHubState();
        var storage = new DeliveryAckHubStorage(state);
        var ctx = new HubContext(NullLogger.Instance, null, typeof(DeliveryAckUnreadableEvent).FullName!, CancellationToken.None);

        foreach (var kind in new[] { "empty-tracking-id", "null-event", "get-event-throws" })
        {
            state.Rows.Clear();
            state.Batches.Clear();
            state.MarkCalls = 0;

            const string subscriberId = "poison-sub";
            var poisonId = kind == "empty-tracking-id" ? Guid.Empty : Guid.NewGuid();
            var goodId = Guid.NewGuid();
            var poison = new DeliveryAckRecord
            {
                SubscriberID = subscriberId,
                TrackingID = poisonId,
                EventType = typeof(DeliveryAckUnreadableEvent).FullName!,
                ExpireOn = DateTime.UtcNow.AddHours(1)
            };

            if (kind == "empty-tracking-id")
                poison.Event = new DeliveryAckUnreadableEvent { EventID = 1 };
            else if (kind == "get-event-throws")
                poison.Event = "not-an-event";

            state.Rows.Add(poison);
            state.Rows.Add(
                new()
                {
                    SubscriberID = subscriberId,
                    TrackingID = goodId,
                    EventType = typeof(DeliveryAckUnreadableEvent).FullName!,
                    Event = new DeliveryAckUnreadableEvent { EventID = 9 },
                    ExpireOn = DateTime.UtcNow.AddHours(1)
                });

            var writer = new TestServerStreamWriter<EventDelivery<DeliveryAckUnreadableEvent>>();
            var reader = new PendingAckReader();
            var registry = new SubscriberRegistry();

            using var cts = new CancellationTokenSource();
            var call = EventDeliveryAckDispatcher.RunAsync<DeliveryAckUnreadableEvent, DeliveryAckRecord, DeliveryAckHubStorage>(
                storage, registry, ctx, subscriberId, reader, writer, cts.Token, deserializationRetryDelay: TimeSpan.Zero);

            try
            {
                var timeoutAt = DateTime.UtcNow.AddSeconds(3);

                while (writer.Responses.Count == 0 && !call.IsCompleted && DateTime.UtcNow < timeoutAt)
                    await Task.Delay(20);

                if (writer.Responses.Count == 0)
                {
                    if (call.IsCompleted)
                        await call;

                    writer.Responses.Count.ShouldBe(1, kind);
                }

                var delivery = writer.Responses.Single();
                delivery.TrackingID.ShouldBe(goodId, kind);
                delivery.Event.EventID.ShouldBe(9, kind);
                state.Rows.Single(row => row.TrackingID == poisonId).IsComplete.ShouldBeTrue(kind);
                state.Rows.Single(row => row.TrackingID == goodId).IsComplete.ShouldBeFalse(kind);
                state.Batches.ShouldBeEmpty(kind);

                reader.Send(new() { TrackingID = goodId });
                await WaitUntil(() => state.Rows.Single(row => row.TrackingID == goodId).IsComplete);

                writer.Responses.Count.ShouldBe(1, kind);
                state.MarkCalls.ShouldBe(2, kind);
                state.Rows.ShouldAllBe(row => row.IsComplete, kind);
            }
            finally
            {
                cts.Cancel();
                await call;
                registry.Subscribers[subscriberId].ConnectionCount.ShouldBe(0);
            }
        }
    }

    [Fact]
    public async Task delivery_ack_second_connection_is_rejected_until_the_first_drops()
    {
        await using var session = DeliveryAckSession<DeliveryAckExclusiveEvent>.Create();
        using var firstCts = new CancellationTokenSource();
        using var thirdCts = new CancellationTokenSource();
        var firstReader = new PendingAckReader();
        var secondReader = new PendingAckReader();
        var thirdReader = new PendingAckReader();
        var firstWriter = new TestServerStreamWriter<EventDelivery<DeliveryAckExclusiveEvent>>();
        var secondWriter = new TestServerStreamWriter<EventDelivery<DeliveryAckExclusiveEvent>>();
        var thirdWriter = new TestServerStreamWriter<EventDelivery<DeliveryAckExclusiveEvent>>();

        firstReader.Send(new() { SubscriberID = "exclusive-sub" });
        var first = session.Hub.OnDeliveryAck(session.Hub, firstReader, firstWriter, CreateServerCallContext(firstCts.Token));
        await WaitUntil(() => session.ConnectionCount("exclusive-sub") == 1);

        secondReader.Send(new() { SubscriberID = "exclusive-sub" });
        var rejected = session.Hub.OnDeliveryAck(session.Hub, secondReader, secondWriter, CreateServerCallContext(CancellationToken.None));
        var error = await Should.ThrowAsync<RpcException>(() => rejected);
        error.StatusCode.ShouldBe(StatusCode.FailedPrecondition);
        error.Status.Detail.ShouldBe("subscriber already connected");
        secondWriter.Responses.ShouldBeEmpty();
        first.IsCompleted.ShouldBeFalse();
        session.ConnectionCount("exclusive-sub").ShouldBe(1);

        await session.Hub.BroadcastEventTaskForTesting(new() { EventID = 1 });
        await WaitUntil(() => firstWriter.Responses.Count == 1);
        firstReader.Send(new() { TrackingID = firstWriter.Responses[0].TrackingID });
        await WaitUntil(() => session.HubState.Rows.Single().IsComplete);
        firstWriter.Responses[0].Event.EventID.ShouldBe(1);

        await firstCts.CancelAsync();
        firstReader.Complete();
        await first.WaitAsync(TimeSpan.FromSeconds(3));
        await WaitUntil(() => session.ConnectionCount("exclusive-sub") == 0);

        await session.Hub.BroadcastEventTaskForTesting(new() { EventID = 2 });
        session.HubState.Rows.Count(r => !r.IsComplete).ShouldBe(1);

        thirdReader.Send(new() { SubscriberID = "exclusive-sub" });
        var third = session.Hub.OnDeliveryAck(session.Hub, thirdReader, thirdWriter, CreateServerCallContext(thirdCts.Token));
        await WaitUntil(() => thirdWriter.Responses.Count == 1);
        thirdWriter.Responses[0].Event.EventID.ShouldBe(2);
        thirdReader.Send(new() { TrackingID = thirdWriter.Responses[0].TrackingID });
        await WaitUntil(() => session.HubState.Rows.All(r => r.IsComplete));

        await thirdCts.CancelAsync();
        thirdReader.Complete();
        await third.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task delivery_ack_opt_in_binds_sub_ack_only()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(new DeliveryAckHubState());
        builder.Services.AddHandlerServer();
        await using var app = builder.Build();

        app.MapHandlers<DeliveryAckRecord, DeliveryAckHubStorage>(h => h.RegisterEventHub<DeliveryAckBindEvent>());
        app.MapHandlers<DeliveryAckRecord, DeliveryAckPlainHubStorage>(h => h.RegisterEventHub<DeliveryAckPlainBindEvent>());

        var routes = ((IEndpointRouteBuilder)app).DataSources
                                                  .SelectMany(source => source.Endpoints)
                                                  .OfType<RouteEndpoint>()
                                                  .Select(endpoint => endpoint.RoutePattern.RawText)
                                                  .OfType<string>()
                                                  .ToArray();

        routes.ShouldContain(ServerRoute<DeliveryAckBindEvent>("sub-ack"));
        routes.ShouldNotContain(ServerRoute<DeliveryAckBindEvent>("sub"));
        routes.ShouldContain(ServerRoute<DeliveryAckPlainBindEvent>("sub"));
        routes.ShouldNotContain(ServerRoute<DeliveryAckPlainBindEvent>("sub-ack"));
    }

    [Fact]
    public async Task delivery_ack_subscriber_does_not_open_sub_when_sub_ack_is_unimplemented()
    {
        await using var session = DeliveryAckSession<DeliveryAckUnimplementedEvent>.Create();
        session.Bridge.RejectDuplex = true;

        await session.Connect("unimplemented-sub", waitForConnection: false);

        await WaitUntil(() => session.Bridge.DuplexCalls >= 2, timeoutMs: 12000);
        session.Bridge.ServerStreamingCalls.ShouldBe(0);
        session.Bridge.FullNames.ShouldAllBe(call => call.FullName == session.Bridge.ExpectedFullName && call.Type == MethodType.DuplexStreaming);
        session.SubState.StoreCalls.ShouldBe(0);
    }

    [Fact]
    public async Task delivery_ack_fanout_is_one_store_and_publish_does_not_wait()
    {
        await using var session = DeliveryAckSession<DeliveryAckFanoutEvent>.Create();
        session.SubState.GateStores = true;
        await session.Connect("fan-a");
        await session.Connect("fan-b");

        var published = session.Hub.BroadcastEventTaskForTesting(new() { EventID = 8 });
        (await CompletesWithin(published, TimeSpan.FromSeconds(2))).ShouldBeTrue();
        session.HubState.Batches.Count.ShouldBe(1);
        session.HubState.Batches[0].Count.ShouldBe(2);

        await WaitUntil(() => session.SubState.StoresWaiting >= 2);
        session.SubState.ReleaseStores.TrySetResult();

        await WaitUntil(() => session.HubState.Rows.Count(r => r.IsComplete) == 2 && DeliveryAckCountingHandler<DeliveryAckFanoutEvent>.Runs == 2);
        session.SubState.Rows.Select(r => r.SubscriberID).OrderBy(id => id, StringComparer.Ordinal).ShouldBe(["fan-a", "fan-b"]);
    }

    [Fact]
    public async Task delivery_ack_round_robin_unacked_row_stays_on_the_chosen_subscriber()
    {
        await using var session = DeliveryAckSession<DeliveryAckRoundRobinEvent>.Create(HubMode.RoundRobin);
        session.SubState.GateStores = true;
        await session.Connect("rr-a");
        await session.Connect("rr-b");

        await session.Hub.BroadcastEventTaskForTesting(new() { EventID = 9 });
        session.HubState.Batches.Count.ShouldBe(1);
        session.HubState.Batches[0].Count.ShouldBe(1);

        await WaitUntil(() => session.SubState.StoresWaiting == 1);
        var chosen = session.HubState.Rows.Single().SubscriberID;
        session.SubState.Rows.ShouldBeEmpty();
        await Task.Delay(200);
        session.HubState.Rows.Single().SubscriberID.ShouldBe(chosen);
        session.HubState.Rows.Single().IsComplete.ShouldBeFalse();

        session.SubState.ReleaseStores.TrySetResult();
        await WaitUntil(
            () => session.SubState.Rows.Count == 1 &&
                  session.HubState.Rows.Single().IsComplete &&
                  DeliveryAckCountingHandler<DeliveryAckRoundRobinEvent>.Runs == 1,
            timeoutMs: 15000);
        session.SubState.Rows.Single().SubscriberID.ShouldBe(chosen);
        session.SubState.Rows.Single().TrackingID.ShouldBe(session.HubState.Rows.Single().TrackingID);
        DeliveryAckCountingHandler<DeliveryAckRoundRobinEvent>.Runs.ShouldBe(1);
    }

    [Fact]
    public async Task delivery_ack_mark_complete_failure_stays_pending_until_redelivery()
    {
        await using var session = DeliveryAckSession<DeliveryAckMarkFailEvent>.Create();
        session.HubState.MarkFailuresRemaining = 1;
        session.HubState.OnMarkFailure = () => session.HubState.CancelActiveCall?.Invoke();
        await session.Connect("mark-fail");

        await session.Hub.BroadcastEventTaskForTesting(new() { EventID = 10 });

        await WaitUntil(() => DeliveryAckCountingHandler<DeliveryAckMarkFailEvent>.Runs == 1);
        session.HubState.Rows.Single().IsComplete.ShouldBeFalse();

        await WaitUntil(() => session.HubState.Rows.Single().IsComplete, timeoutMs: 15000);
        session.SubState.Rows.Count.ShouldBe(1);
        session.SubState.DuplicateCount.ShouldBe(1);
        session.HubState.MarkCalls.ShouldBeGreaterThan(1);
        DeliveryAckCountingHandler<DeliveryAckMarkFailEvent>.Runs.ShouldBe(1);
    }

    [Fact]
    public async Task delivery_ack_cleanup_preserves_completed_keys_through_repeated_reconnects()
    {
        await using var session = DeliveryAckSession<DeliveryAckCleanupEvent>.Create();
        session.HubState.MarkFailuresRemaining = int.MaxValue;
        session.HubState.OnMarkFailure = () => session.HubState.CancelActiveCall?.Invoke();
        await session.Connect("cleanup", executionExpiry: TimeSpan.FromMinutes(1));
        await session.Hub.BroadcastEventTaskForTesting(new() { EventID = 11 });
        await WaitUntil(() => session.SubState.Rows.Exists(row => row.IsComplete));
        var trackingId = session.SubState.Rows.Single().TrackingID;
        session.SubState.Rows.Single().RetainUntil.ShouldBe(session.HubState.Rows.Single().ExpireOn.AddMinutes(5));
        session.SubState.Rows.Single().ExpireOn = DateTime.UtcNow.AddMinutes(-1);
        var storage = new DeliveryAckSubscriberStorage(session.SubState);

        for (var i = 1; i <= 2; i++)
        {
            await WaitUntil(() => session.HubState.MarkCalls >= i);
            await storage.PurgeStaleRecordsAsync(new()
            {
                Match = EventSubscriberRetentionPolicy<DeliveryAckRecord>.PurgeMatch
            });
            session.SubState.Rows.Count.ShouldBe(1);
            session.HubState.CancelActiveCall!.Invoke();
            await WaitUntil(() => session.SubState.DuplicateCount >= i, timeoutMs: 15000);
        }

        session.HubState.MarkFailuresRemaining = 0;
        await WaitUntil(() => session.HubState.Rows.Single().IsComplete, timeoutMs: 15000);
        session.SubState.Rows.Single().TrackingID.ShouldBe(trackingId);
        session.SubState.StoreCalls.ShouldBe(1);
        DeliveryAckCountingHandler<DeliveryAckCleanupEvent>.Runs.ShouldBe(1);
    }

    [Fact]
    public async Task delivery_ack_cleanup_respects_short_execution_expiry_and_mixed_rows()
    {
        var now = DateTime.UtcNow;
        var state = new DeliveryAckSubscriberState();
        state.Rows.AddRange([
            new() { TrackingID = Guid.NewGuid(), IsComplete = true, ExpireOn = now.AddHours(1) },
            new() { TrackingID = Guid.NewGuid(), ExpireOn = now.AddMinutes(-1) },
            new() { TrackingID = Guid.NewGuid(), IsComplete = true, ExpireOn = now.AddMinutes(-1), RetainUntil = now.AddHours(4) },
            new() { TrackingID = Guid.NewGuid(), ExpireOn = now.AddMinutes(-1), RetainUntil = now.AddHours(4) },
            new() { TrackingID = Guid.NewGuid(), IsComplete = true, ExpireOn = now.AddHours(1), RetainUntil = now.AddMinutes(-1) }
        ]);
        var storage = new DeliveryAckSubscriberStorage(state);
        await storage.PurgeStaleRecordsAsync(new()
        {
            Match = EventSubscriberRetentionPolicy<DeliveryAckRecord>.PurgeMatch
        });
        state.Rows.Count.ShouldBe(2);
        state.Rows.ShouldAllBe(row => row.RetainUntil > now);
        var pending = await storage.GetNextBatchAsync(new()
        {
            Match = row => !row.IsComplete && DateTime.UtcNow <= row.ExpireOn,
            Limit = 10
        });
        pending.ShouldBeEmpty();
        foreach (var row in state.Rows)
            row.RetainUntil = now.AddMinutes(-1);
        await storage.PurgeStaleRecordsAsync(new()
        {
            Match = EventSubscriberRetentionPolicy<DeliveryAckRecord>.PurgeMatch
        });
        state.Rows.ShouldBeEmpty();
    }

    [Fact]
    public void delivery_ack_clock_skew_allowance_requires_retention_capable_records()
    {
        var storage = new BatchDequeueEventSubscriberStorage();
        var error = Should.Throw<InvalidOperationException>(() => EventSubscriberRetentionPolicy<TestEventRecord>.GetClockSkewAllowance(storage));
        error.Message.ShouldBe("ACK inbox records must implement IEventDeliveryAckStorageRecord and persist RetainUntil.");
    }

    [Fact]
    public void delivery_ack_retention_deadline_includes_clock_skew_allowance()
    {
        var replayUntil = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        var allowance = TimeSpan.FromMinutes(12);
        var retainUntil = EventSubscriberRetentionPolicy<DeliveryAckRecord>.GetRetainUntil(replayUntil, allowance);

        retainUntil.ShouldBe(replayUntil.AddMinutes(12));
        retainUntil.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void delivery_ack_clock_skew_allowance_is_configurable_and_non_negative()
    {
        var state = new DeliveryAckSubscriberState();
        var storage = new DeliveryAckSubscriberStorage(state);
        EventSubscriberRetentionPolicy<DeliveryAckRecord>.GetClockSkewAllowance(storage).ShouldBe(TimeSpan.FromMinutes(5));
        state.ClockSkewAllowance = TimeSpan.FromMinutes(12);
        EventSubscriberRetentionPolicy<DeliveryAckRecord>.GetClockSkewAllowance(storage).ShouldBe(TimeSpan.FromMinutes(12));
        state.ClockSkewAllowance = TimeSpan.FromSeconds(-1);
        Should.Throw<ArgumentOutOfRangeException>(() => EventSubscriberRetentionPolicy<DeliveryAckRecord>.GetClockSkewAllowance(storage));
    }

    [Fact]
    public void delivery_ack_replay_deadline_survives_messagepack_round_trip()
    {
        var delivery = new EventDelivery<DeliveryAckCleanupEvent>
        {
            TrackingID = Guid.NewGuid(),
            Event = new() { EventID = 13 },
            ReplayUntil = DateTime.UtcNow.AddHours(4)
        };
        var options = MessagePack.MessagePackSerializerOptions.Standard
                                 .WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance)
                                 .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray);
        var copy = MessagePack.MessagePackSerializer.Deserialize<EventDelivery<DeliveryAckCleanupEvent>>(
            MessagePack.MessagePackSerializer.Serialize(delivery, options), options);
        copy.TrackingID.ShouldBe(delivery.TrackingID);
        copy.ReplayUntil.ShouldBe(delivery.ReplayUntil);
        copy.Event.EventID.ShouldBe(13);
    }

    [Fact]
    public async Task delivery_ack_missing_replay_deadline_stops_without_storing_or_acknowledging()
    {
        await using var session = DeliveryAckSession<DeliveryAckLegacyHubEvent>.Create();
        session.Bridge.OmitReplayDeadline = true;
        await session.Connect("legacy-hub");
        await session.Hub.BroadcastEventTaskForTesting(new() { EventID = 12 });
        await WaitUntil(() => session.ConnectionCount("legacy-hub") == 0);
        session.SubState.Rows.ShouldBeEmpty();
        session.HubState.Rows.Single().IsComplete.ShouldBeFalse();
        session.HubState.MarkCalls.ShouldBe(0);
        session.Bridge.DuplexCalls.ShouldBe(1);
        DeliveryAckCountingHandler<DeliveryAckLegacyHubEvent>.Runs.ShouldBe(0);
    }

    public sealed class DeliveryAckLegacyHubEvent : IEvent
    {
        public int EventID { get; set; }
    }

    public sealed class DeliveryAckCleanupEvent : IEvent
    {
        public int EventID { get; set; }
    }

    [Fact]
    public void subscribe_core_picks_ack_subscriber_only_when_the_provider_opts_in()
    {
        var provider = CreateServiceProvider(services => services.AddSingleton(new DeliveryAckSubscriberState()));
        RemoteConnectionCore.StorageRecordType = typeof(DeliveryAckRecord);
        RemoteConnectionCore.StorageProviderType = typeof(DeliveryAckSubscriberStorage);

        try
        {
            var connection = new TestRemoteConnectionCore("http://localhost:5001", provider);
            connection.Subscribe<DeliveryAckSubscribeEvent, DeliveryAckCountingHandler<DeliveryAckSubscribeEvent>>(new CancellationToken(true));
            connection.GetExecutor(typeof(DeliveryAckCountingHandler<DeliveryAckSubscribeEvent>))
                      .ShouldBeOfType<EventDeliveryAckSubscriber<DeliveryAckSubscribeEvent, DeliveryAckCountingHandler<DeliveryAckSubscribeEvent>, DeliveryAckRecord, DeliveryAckSubscriberStorage>>();
        }
        finally
        {
            RemoteConnectionCore.StorageRecordType = typeof(InMemoryEventStorageRecord);
            RemoteConnectionCore.StorageProviderType = typeof(InMemoryEventSubscriberStorage);
        }

        RemoteConnectionCore.StorageRecordType = typeof(DeliveryAckRecord);
        RemoteConnectionCore.StorageProviderType = typeof(DeliveryAckPlainSubscriberStorage);

        try
        {
            var plainProvider = CreateServiceProvider();
            var connection = new TestRemoteConnectionCore("http://localhost:5001", plainProvider);
            connection.Subscribe<DeliveryAckPlainSubscribeEvent, DeliveryAckCountingHandler<DeliveryAckPlainSubscribeEvent>>(new CancellationToken(true));
            connection.GetExecutor(typeof(DeliveryAckCountingHandler<DeliveryAckPlainSubscribeEvent>))
                      .ShouldBeOfType<EventSubscriber<DeliveryAckPlainSubscribeEvent, DeliveryAckCountingHandler<DeliveryAckPlainSubscribeEvent>, DeliveryAckRecord, DeliveryAckPlainSubscriberStorage>>();
        }
        finally
        {
            RemoteConnectionCore.StorageRecordType = typeof(InMemoryEventStorageRecord);
            RemoteConnectionCore.StorageProviderType = typeof(InMemoryEventSubscriberStorage);
        }
    }

    [Fact]
    public void delivery_ack_default_subscriber_id_matches_the_non_ack_subscriber()
    {
        var provider = CreateServiceProvider(services => services.AddSingleton(new DeliveryAckSubscriberState()));
        using var channel = GrpcChannel.ForAddress("http://localhost:5091");

        var plain = new EventSubscriber<DeliveryAckStableIdEvent, DeliveryAckCountingHandler<DeliveryAckStableIdEvent>, DeliveryAckRecord, DeliveryAckSubscriberStorage>(
            channel,
            clientIdentifier: "client-stable",
            subscriberID: null,
            serviceProvider: provider,
            marshaller: MessagePackMarshallerFactory.Instance);
        var ack = new EventDeliveryAckSubscriber<DeliveryAckStableIdEvent, DeliveryAckCountingHandler<DeliveryAckStableIdEvent>, DeliveryAckRecord, DeliveryAckSubscriberStorage>(
            channel,
            clientIdentifier: "client-stable",
            subscriberID: null,
            serviceProvider: provider,
            marshaller: MessagePackMarshallerFactory.Instance);

        GetEventSubscriberID(ack).ShouldBe(GetEventSubscriberID(plain));

        var explicitPlain = new EventSubscriber<DeliveryAckStableIdEvent, DeliveryAckCountingHandler<DeliveryAckStableIdEvent>, DeliveryAckRecord, DeliveryAckSubscriberStorage>(
            channel,
            clientIdentifier: "client-stable",
            subscriberID: " kept-id ",
            serviceProvider: provider,
            marshaller: MessagePackMarshallerFactory.Instance);
        var explicitAck = new EventDeliveryAckSubscriber<DeliveryAckStableIdEvent, DeliveryAckCountingHandler<DeliveryAckStableIdEvent>, DeliveryAckRecord, DeliveryAckSubscriberStorage>(
            channel,
            clientIdentifier: "client-stable",
            subscriberID: " kept-id ",
            serviceProvider: provider,
            marshaller: MessagePackMarshallerFactory.Instance);

        GetEventSubscriberID(explicitPlain).ShouldBe("kept-id");
        GetEventSubscriberID(explicitAck).ShouldBe("kept-id");
    }

    [Fact]
    public async Task delivery_ack_opt_in_drains_the_pre_opt_in_backlog()
    {
        await using var session = DeliveryAckSession<DeliveryAckStableBacklogEvent>.Create();
        var preOptInId = SubscriberIDFactory.Create(
            null,
            "client",
            typeof(EventSubscriber<DeliveryAckStableBacklogEvent, DeliveryAckCountingHandler<DeliveryAckStableBacklogEvent>, DeliveryAckRecord, DeliveryAckSubscriberStorage>),
            session.Bridge.Target);
        var trackingId = Guid.NewGuid();
        var eventType = typeof(DeliveryAckStableBacklogEvent).FullName!;
        var backlog = new DeliveryAckStableBacklogEvent { EventID = 7 };

        // same tracking id: the hub still holds the delivery, and the subscriber already stored it but has not run the handler.
        session.HubState.Rows.Add(
            new()
            {
                SubscriberID = preOptInId,
                TrackingID = trackingId,
                EventType = eventType,
                Event = backlog,
                ExpireOn = DateTime.UtcNow.AddHours(1)
            });
        session.SubState.Rows.Add(
            new()
            {
                SubscriberID = preOptInId,
                TrackingID = trackingId,
                EventType = eventType,
                Event = backlog,
                ExpireOn = DateTime.UtcNow.AddHours(1)
            });

        var liveId = await session.ConnectWithoutExplicitId();
        liveId.ShouldBe(preOptInId);

        await WaitUntil(
            () => session.HubState.Rows.Single(r => r.TrackingID == trackingId).IsComplete &&
                  session.SubState.Rows.Single(r => r.TrackingID == trackingId).IsComplete &&
                  DeliveryAckCountingHandler<DeliveryAckStableBacklogEvent>.Runs == 1,
            timeoutMs: 15000);

        session.SubState.Rows.Count.ShouldBe(1);
        session.SubState.DuplicateCount.ShouldBe(1);
        session.Bridge.ServerStreamingCalls.ShouldBe(0);
        session.Bridge.DuplexCalls.ShouldBeGreaterThan(0);
        session.Bridge.FullNames.ShouldAllBe(call => call.FullName == session.Bridge.ExpectedFullName && call.Type == MethodType.DuplexStreaming);

        await session.Hub.BroadcastEventTaskForTesting(new() { EventID = 8 });

        await WaitUntil(
            () => DeliveryAckCountingHandler<DeliveryAckStableBacklogEvent>.Runs == 2 &&
                  session.HubState.Rows.Count(r => r.IsComplete) == 2 &&
                  session.SubState.Rows.Count(r => r.IsComplete) == 2,
            timeoutMs: 15000);

        session.HubState.Rows.Single(r => r.TrackingID != trackingId).SubscriberID.ShouldBe(preOptInId);
        session.SubState.Rows.Single(r => r.TrackingID != trackingId).SubscriberID.ShouldBe(preOptInId);
    }

    static string ServerRoute<TEvent>(string method) where TEvent : class
        => "/" + typeof(TEvent).FullName + "/" + method;

    sealed class DeliveryAckHappyEvent : IEvent { public int EventID { get; set; } }
    sealed class DeliveryAckDropAfterStoreEvent : IEvent { public int EventID { get; set; } }
    sealed class DeliveryAckDropBeforeStoreEvent : IEvent { public int EventID { get; set; } }
    sealed class DeliveryAckMismatchEvent : IEvent { public int EventID { get; set; } }
    sealed class DeliveryAckExclusiveEvent : IEvent { public int EventID { get; set; } }
    sealed class DeliveryAckBindEvent : IEvent { public int EventID { get; set; } }
    sealed class DeliveryAckPlainBindEvent : IEvent { public int EventID { get; set; } }
    sealed class DeliveryAckUnimplementedEvent : IEvent { public int EventID { get; set; } }
    sealed class DeliveryAckFanoutEvent : IEvent { public int EventID { get; set; } }
    sealed class DeliveryAckRoundRobinEvent : IEvent { public int EventID { get; set; } }
    sealed class DeliveryAckMarkFailEvent : IEvent { public int EventID { get; set; } }
    sealed class DeliveryAckSubscribeEvent : IEvent { public int EventID { get; set; } }
    sealed class DeliveryAckPlainSubscribeEvent : IEvent { public int EventID { get; set; } }
    sealed class DeliveryAckStableIdEvent : IEvent { public int EventID { get; set; } }
    sealed class DeliveryAckStableBacklogEvent : IEvent { public int EventID { get; set; } }

    sealed class DeliveryAckUnreadableEvent : IEvent { public int EventID { get; set; } }

    sealed class DeliveryAckCountingHandler<TEvent> : IEventHandler<TEvent> where TEvent : class, IEvent
    {
        public static int Runs;
        public static Func<CancellationToken, Task>? OnHandle;

        public Task HandleAsync(TEvent evnt, CancellationToken ct)
        {
            Interlocked.Increment(ref Runs);

            return OnHandle?.Invoke(ct) ?? Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task subscriber_adapter_start_persists_and_executes_event(bool ack)
        => ack ? VerifySubscriberAdapterStartup<DeliveryAckAdapterEvent>(true) : VerifySubscriberAdapterStartup<PlainAdapterEvent>(false);

    static async Task VerifySubscriberAdapterStartup<TEvent>(bool ack) where TEvent : class, IEvent, new()
    {
        await using var session = DeliveryAckSession<TEvent>.Create();
        session.Bridge.AllowServerStreaming = !ack;
        const string subscriberId = "adapter-start";
        IEventSubscriber subscriber = ack
                                          ? new EventDeliveryAckSubscriber<TEvent, DeliveryAckCountingHandler<TEvent>, DeliveryAckRecord, DeliveryAckSubscriberStorage>(
                                              session.Bridge, "client", subscriberId, session.Provider, MessagePackMarshallerFactory.Instance)
                                          : new EventSubscriber<TEvent, DeliveryAckCountingHandler<TEvent>, DeliveryAckRecord, DeliveryAckSubscriberStorage>(
                                              session.Bridge, "client", subscriberId, session.Provider, MessagePackMarshallerFactory.Instance);
        subscriber.Start(new(cancellationToken: session.Cts.Token));
        await WaitUntil(() => session.ConnectionCount(subscriberId) == 1);
        await session.Hub.BroadcastEventTaskForTesting(new TEvent());
        await WaitUntil(() => session.SubState.Rows.Count == 1 && session.SubState.Rows.Single().IsComplete && session.HubState.Rows.Single().IsComplete);
        DeliveryAckCountingHandler<TEvent>.Runs.ShouldBe(1);
        session.SubState.Rows.Single().SubscriberID.ShouldBe(subscriberId);
        session.SubState.Rows.Single().EventType.ShouldBe(typeof(TEvent).FullName);
        ((IEventStorageRecord)session.SubState.Rows.Single()).GetEvent<TEvent>().ShouldNotBeNull();
        session.Bridge.DuplexCalls.ShouldBe(ack ? 1 : 0);
        session.Bridge.ServerStreamingCalls.ShouldBe(ack ? 0 : 1);
        session.Cts.Cancel();
        await WaitUntil(() => session.ConnectionCount(subscriberId) == 0);
    }

    sealed class DeliveryAckAdapterEvent : IEvent;
    sealed class PlainAdapterEvent : IEvent;

    sealed class DeliveryAckSession<TEvent> : IAsyncDisposable where TEvent : class, IEvent
    {
        readonly List<Task> _workers = [];
        readonly List<SemaphoreSlim> _semaphores = [];

        public DeliveryAckHubState HubState { get; }
        public DeliveryAckSubscriberState SubState { get; }
        public IServiceProvider Provider { get; }
        public EventHub<TEvent, DeliveryAckRecord, DeliveryAckHubStorage> Hub { get; }
        public DeliveryAckBridge<TEvent> Bridge { get; }
        public CancellationTokenSource Cts { get; }

        DeliveryAckSession(DeliveryAckHubState hubState,
                           DeliveryAckSubscriberState subState,
                           IServiceProvider provider,
                           EventHub<TEvent, DeliveryAckRecord, DeliveryAckHubStorage> hub,
                           DeliveryAckBridge<TEvent> bridge,
                           CancellationTokenSource cts)
        {
            HubState = hubState;
            SubState = subState;
            Provider = provider;
            Hub = hub;
            Bridge = bridge;
            Cts = cts;
        }

        public static DeliveryAckSession<TEvent> Create(HubMode mode = HubMode.EventPublisher)
        {
            DeliveryAckCountingHandler<TEvent>.Runs = 0;
            DeliveryAckCountingHandler<TEvent>.OnHandle = null;
            var hubState = new DeliveryAckHubState();
            var subState = new DeliveryAckSubscriberState();
            var provider = CreateServiceProvider(services =>
            {
                services.AddSingleton(hubState);
                services.AddSingleton(subState);
            });
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            EventHub<TEvent, DeliveryAckRecord, DeliveryAckHubStorage>.Mode = mode;
            var hub = new EventHub<TEvent, DeliveryAckRecord, DeliveryAckHubStorage>(provider);
            var bridge = new DeliveryAckBridge<TEvent>(hub, hubState, cts.Token);

            return new(hubState, subState, provider, hub, bridge, cts);
        }

        public async Task Connect(string subscriberId, bool waitForConnection = true, TimeSpan? executionExpiry = null)
        {
            var storage = new DeliveryAckSubscriberStorage(SubState);
            var sem = new SemaphoreSlim(0);
            _semaphores.Add(sem);
            var opts = new CallOptions(cancellationToken: Cts.Token);
            var eventTypeName = typeof(TEvent).FullName!;
            var logger = GetSubscriberLogger<TEvent, DeliveryAckCountingHandler<TEvent>, DeliveryAckRecord, DeliveryAckSubscriberStorage>(Provider);
            var method = new Method<EventDeliveryAck, EventDelivery<TEvent>>(
                MethodType.DuplexStreaming,
                eventTypeName + "/sub-ack",
                "",
                new MessagePackMarshaller<EventDeliveryAck>(),
                new MessagePackMarshaller<EventDelivery<TEvent>>());

            _workers.Add(EventDeliveryAckReceiver.RunAsync<TEvent, DeliveryAckRecord, DeliveryAckSubscriberStorage>(
                storage,
                SubscriberStorageBehavior.Durable,
                sem,
                opts,
                Bridge.Invoker,
                method,
                subscriberId,
                eventTypeName,
                executionExpiry ?? _defaultRecordExpiry,
                logger,
                errors: null,
                retryDelay: TimeSpan.FromMilliseconds(50)));

            _workers.Add(EventExecutorWorker.RunAsync<TEvent, DeliveryAckCountingHandler<TEvent>, DeliveryAckRecord, DeliveryAckSubscriberStorage>(
                storage,
                SubscriberStorageBehavior.Durable,
                sem,
                opts,
                Environment.ProcessorCount,
                subscriberId,
                eventTypeName,
                logger,
                ActivatorUtilities.CreateFactory(typeof(DeliveryAckCountingHandler<TEvent>), Type.EmptyTypes),
                Provider,
                errorReceiver: null));

            if (waitForConnection)
                await WaitUntil(() => ConnectionCount(subscriberId) == 1);
        }

        public async Task<string> ConnectWithoutExplicitId()
        {
            var subscriber = new EventDeliveryAckSubscriber<TEvent, DeliveryAckCountingHandler<TEvent>, DeliveryAckRecord, DeliveryAckSubscriberStorage>(
                Bridge,
                clientIdentifier: "client",
                subscriberID: null,
                serviceProvider: Provider,
                marshaller: MessagePackMarshallerFactory.Instance);
            var subscriberId = GetEventSubscriberID(subscriber);
            subscriber.Start(new(cancellationToken: Cts.Token));
            await WaitUntil(() => ConnectionCount(subscriberId) == 1);

            return subscriberId;
        }

        public int ConnectionCount(string subscriberId)
        {
            var hubType = typeof(EventHub<TEvent, DeliveryAckRecord, DeliveryAckHubStorage>);

            if (!TryGetSubscriberFromHub(hubType, subscriberId, out var subscriber))
                return 0;

            return (int)subscriber!.GetType().GetProperty("ConnectionCount")!.GetValue(subscriber)!;
        }

        public async ValueTask DisposeAsync()
        {
            SubState.ReleaseStores.TrySetResult();
            Cts.Cancel();
            await Task.WhenAll(_workers).WaitAsync(TimeSpan.FromSeconds(5));

            foreach (var sem in _semaphores)
                sem.Dispose();

            Bridge.Dispose();
        }
    }

    sealed class DeliveryAckBridge<TEvent> : ChannelBase, IDisposable where TEvent : class, IEvent
    {
        readonly Lock _gate = new();
        readonly List<ObservedCall> _fullNames = [];
        readonly EventHub<TEvent, DeliveryAckRecord, DeliveryAckHubStorage> _hub;
        readonly DeliveryAckHubState _hubState;
        readonly CancellationToken _lifetime;
        int _duplexCalls;
        int _serverStreamingCalls;
        int _dropAckCalls;
        bool _failFirstRead;

        public DeliveryAckBridge(EventHub<TEvent, DeliveryAckRecord, DeliveryAckHubStorage> hub, DeliveryAckHubState hubState, CancellationToken lifetime)
            : base("delivery-ack")
        {
            _hub = hub;
            _hubState = hubState;
            _lifetime = lifetime;
            Invoker = new BridgeInvoker(this);
        }

        public CallInvoker Invoker { get; }
        public bool RejectDuplex { get; set; }
        public bool AllowServerStreaming { get; set; }
        public bool OmitReplayDeadline { get; set; }
        public bool GateAcks { get; set; }
        public TaskCompletionSource AckWaiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseAcks { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int DropAckCalls { get => _dropAckCalls; set => _dropAckCalls = value; }
        public bool FailFirstDeliveryRead { get => _failFirstRead; set => _failFirstRead = value; }
        public int DuplexCalls => Volatile.Read(ref _duplexCalls);
        public int ServerStreamingCalls => Volatile.Read(ref _serverStreamingCalls);
        public string ExpectedFullName => "/" + typeof(TEvent).FullName + "/sub-ack/";

        public ObservedCall[] FullNames
        {
            get
            {
                lock (_gate)
                    return _fullNames.ToArray();
            }
        }

        public readonly record struct ObservedCall(string FullName, MethodType Type);

        public override CallInvoker CreateCallInvoker()
            => Invoker;

        public void Dispose() { }

        bool TakeDropAck()
        {
            lock (_gate)
            {
                if (_dropAckCalls <= 0)
                    return false;

                _dropAckCalls--;

                return true;
            }
        }

        bool TakeFailRead()
        {
            lock (_gate)
            {
                if (!_failFirstRead)
                    return false;

                _failFirstRead = false;

                return true;
            }
        }

        void NoteDuplex(string fullName, MethodType type)
        {
            Interlocked.Increment(ref _duplexCalls);
            lock (_gate)
                _fullNames.Add(new(fullName, type));
        }

        void NoteServerStreaming()
            => Interlocked.Increment(ref _serverStreamingCalls);

        sealed class BridgeInvoker(DeliveryAckBridge<TEvent> bridge) : CallInvoker
        {
            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                                                        string? host,
                                                                                                                        CallOptions options)
            {
                bridge.NoteDuplex(method.FullName, method.Type);

                if (bridge.RejectDuplex || method.Type != MethodType.DuplexStreaming || method.FullName != bridge.ExpectedFullName)
                    throw new RpcException(new Status(StatusCode.Unimplemented, "unimplemented"));

                var requests = Channel.CreateUnbounded<EventDeliveryAck>();
                var responses = Channel.CreateUnbounded<EventDelivery<TEvent>>();
                var callCts = CancellationTokenSource.CreateLinkedTokenSource(bridge._lifetime);
                bridge._hubState.CancelActiveCall = () => callCts.Cancel();

                var serverTask = bridge._hub.OnDeliveryAck(
                    bridge._hub,
                    new AckReader(requests),
                    new DeliveryWriter(responses, bridge.OmitReplayDeadline),
                    CreateServerCallContext(callCts.Token));
                _ = serverTask.ContinueWith(
                    task =>
                    {
                        if (task.IsFaulted)
                            responses.Writer.TryComplete(task.Exception!.GetBaseException());
                        else
                            responses.Writer.TryComplete();
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                return new(
                    (IClientStreamWriter<TRequest>)(object)new AckWriter(requests, bridge.TakeDropAck(), bridge),
                    (IAsyncStreamReader<TResponse>)(object)new DeliveryReader(responses, requests, bridge.TakeFailRead()),
                    Task.FromResult(new Metadata()),
                    static () => Status.DefaultSuccess,
                    static () => new Metadata(),
                    () =>
                    {
                        callCts.Cancel();
                        requests.Writer.TryComplete();
                    });
            }

            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                                              string? host,
                                                                                                              CallOptions options,
                                                                                                              TRequest request)
            {
                bridge.NoteServerStreaming();

                if (!bridge.AllowServerStreaming)
                    throw new RpcException(new Status(StatusCode.Unimplemented, "unimplemented"));

                var responses = Channel.CreateUnbounded<EventDelivery<TEvent>>();
                var requests = Channel.CreateUnbounded<EventDeliveryAck>();
                var serverTask = bridge._hub.OnSubscriberConnected(
                    bridge._hub,
                    (string)(object)request!,
                    new OrdinaryDeliveryWriter(responses),
                    CreateServerCallContext(options.CancellationToken));
                _ = serverTask.ContinueWith(
                    task => responses.Writer.TryComplete(task.Exception?.GetBaseException()),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                return new(
                    (IAsyncStreamReader<TResponse>)(object)new OrdinaryDeliveryReader(new DeliveryReader(responses, requests, false)),
                    Task.FromResult(new Metadata()),
                    static () => Status.DefaultSuccess,
                    static () => new Metadata(),
                    static () => { });
            }

            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
                                                                                                                        string? host,
                                                                                                                        CallOptions options)
                => throw new NotSupportedException();

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

        sealed class AckWriter(Channel<EventDeliveryAck> requests, bool dropAck, DeliveryAckBridge<TEvent> bridge) : IClientStreamWriter<EventDeliveryAck>
        {
            int _writes;

            public WriteOptions? WriteOptions { get; set; }

            public async Task WriteAsync(EventDeliveryAck message)
            {
                var write = Interlocked.Increment(ref _writes);

                if (bridge.GateAcks && write > 1)
                {
                    bridge.AckWaiting.TrySetResult();
                    await bridge.ReleaseAcks.Task.WaitAsync(bridge._lifetime);
                }

                if (dropAck && write > 1)
                {
                    requests.Writer.TryComplete(new InvalidOperationException("dropped after store"));

                    throw new InvalidOperationException("dropped after store");
                }

                await requests.Writer.WriteAsync(message);
            }

            public Task WriteAsync(EventDeliveryAck message, CancellationToken cancellationToken)
                => WriteAsync(message);

            public Task CompleteAsync()
            {
                requests.Writer.TryComplete();

                return Task.CompletedTask;
            }
        }

        sealed class AckReader(Channel<EventDeliveryAck> requests) : IAsyncStreamReader<EventDeliveryAck>
        {
            public EventDeliveryAck Current { get; private set; } = null!;

            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                if (!await requests.Reader.WaitToReadAsync(cancellationToken))
                    return false;

                if (!requests.Reader.TryRead(out var message))
                    return false;

                Current = message;

                return true;
            }
        }

        sealed class OrdinaryDeliveryWriter(Channel<EventDelivery<TEvent>> responses) : IServerStreamWriter<TEvent>
        {
            public WriteOptions? WriteOptions { get; set; }

            public Task WriteAsync(TEvent message)
                => responses.Writer.WriteAsync(new() { Event = message }).AsTask();

            public Task WriteAsync(TEvent message, CancellationToken cancellationToken)
                => responses.Writer.WriteAsync(new() { Event = message }, cancellationToken).AsTask();
        }

        sealed class OrdinaryDeliveryReader(DeliveryReader reader) : IAsyncStreamReader<TEvent>
        {
            public TEvent Current => reader.Current.Event;

            public Task<bool> MoveNext(CancellationToken cancellationToken)
                => reader.MoveNext(cancellationToken);
        }

        sealed class DeliveryWriter(Channel<EventDelivery<TEvent>> responses, bool omitReplayDeadline) : IServerStreamWriter<EventDelivery<TEvent>>
        {
            public WriteOptions? WriteOptions { get; set; }

            public Task WriteAsync(EventDelivery<TEvent> message)
            {
                if (omitReplayDeadline)
                    message.ReplayUntil = default;
                return responses.Writer.WriteAsync(message).AsTask();
            }

            public Task WriteAsync(EventDelivery<TEvent> message, CancellationToken cancellationToken)
                => WriteAsync(message);
        }

        sealed class DeliveryReader(Channel<EventDelivery<TEvent>> responses, Channel<EventDeliveryAck> requests, bool failRead) : IAsyncStreamReader<EventDelivery<TEvent>>
        {
            public EventDelivery<TEvent> Current { get; private set; } = null!;

            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                if (!await responses.Reader.WaitToReadAsync(cancellationToken))
                    return false;

                if (failRead)
                {
                    requests.Writer.TryComplete(new InvalidOperationException("dropped before store"));

                    throw new InvalidOperationException("dropped before store");
                }

                if (!responses.Reader.TryRead(out var message))
                    return false;

                Current = message;

                return true;
            }
        }
    }

    sealed class ScriptedAckReader(params EventDeliveryAck[] messages) : IAsyncStreamReader<EventDeliveryAck>
    {
        int _index = -1;

        public EventDeliveryAck Current { get; private set; } = null!;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<bool>(cancellationToken);

            _index++;

            if (_index >= messages.Length)
                return Task.FromResult(false);

            Current = messages[_index];

            return Task.FromResult(true);
        }
    }

    sealed class PendingAckReader : IAsyncStreamReader<EventDeliveryAck>
    {
        readonly Channel<EventDeliveryAck> _channel = Channel.CreateUnbounded<EventDeliveryAck>();

        public EventDeliveryAck Current { get; private set; } = null!;

        public void Send(EventDeliveryAck ack)
            => _channel.Writer.TryWrite(ack);

        public void Complete()
            => _channel.Writer.TryComplete();

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (!await _channel.Reader.WaitToReadAsync(cancellationToken))
                return false;

            if (!_channel.Reader.TryRead(out var ack))
                return false;

            Current = ack;

            return true;
        }
    }

    public sealed class DeliveryAckRecord : IEventDeliveryAckStorageRecord
    {
        public string SubscriberID { get; set; } = "";
        public Guid TrackingID { get; set; }
        public object Event { get; set; } = null!;
        public string EventType { get; set; } = "";
        public DateTime ExpireOn { get; set; }
        public DateTime? RetainUntil { get; set; }
        public bool IsComplete { get; set; }

        public static DeliveryAckRecord Clone(DeliveryAckRecord record)
            => new()
            {
                SubscriberID = record.SubscriberID,
                TrackingID = record.TrackingID,
                Event = record.Event,
                EventType = record.EventType,
                ExpireOn = record.ExpireOn,
                RetainUntil = record.RetainUntil,
                IsComplete = record.IsComplete
            };
    }

    public sealed class DeliveryAckHubState
    {
        public Lock Sync { get; } = new();
        public List<DeliveryAckRecord> Rows { get; } = [];
        public List<List<DeliveryAckRecord>> Batches { get; } = [];
        public int MarkCalls;
        public int MarkFailuresRemaining;
        public TimeSpan AckTimeout = TimeSpan.FromSeconds(30);
        public Action? OnMarkFailure;
        public Action? CancelActiveCall;
    }

    public sealed class DeliveryAckHubStorage(DeliveryAckHubState state) : IEventHubDeliveryAck<DeliveryAckRecord>
    {
        public TimeSpan DeliveryAckTimeout => state.AckTimeout;

        public ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<DeliveryAckRecord> parameters)
            => new(Array.Empty<string>());

        public ValueTask StoreEventsAsync(IEnumerable<DeliveryAckRecord> records, CancellationToken ct)
        {
            lock (state.Sync)
            {
                var batch = records.Select(DeliveryAckRecord.Clone).ToList();
                state.Batches.Add(batch);
                state.Rows.AddRange(batch);
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask<IEnumerable<DeliveryAckRecord>> GetNextBatchAsync(PendingRecordSearchParams<DeliveryAckRecord> parameters)
        {
            var match = parameters.Match.Compile();

            lock (state.Sync)
                return new(state.Rows.Where(match).Take(parameters.Limit).Select(DeliveryAckRecord.Clone).ToList());
        }

        public ValueTask MarkEventAsCompleteAsync(DeliveryAckRecord record, CancellationToken ct)
        {
            Action? fail;

            lock (state.Sync)
            {
                state.MarkCalls++;

                if (state.MarkFailuresRemaining > 0)
                {
                    state.MarkFailuresRemaining--;
                    fail = state.OnMarkFailure;
                }
                else
                {
                    state.Rows.First(row => row.TrackingID == record.TrackingID).IsComplete = true;

                    return ValueTask.CompletedTask;
                }
            }

            fail?.Invoke();

            throw new InvalidOperationException("mark failed");
        }

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<DeliveryAckRecord> parameters)
            => ValueTask.CompletedTask;
    }

    public sealed class DeliveryAckPlainHubStorage : IEventHubStorageProvider<DeliveryAckRecord>
    {
        public ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<DeliveryAckRecord> parameters)
            => new(Array.Empty<string>());

        public ValueTask StoreEventsAsync(IEnumerable<DeliveryAckRecord> records, CancellationToken ct)
            => ValueTask.CompletedTask;

        public ValueTask<IEnumerable<DeliveryAckRecord>> GetNextBatchAsync(PendingRecordSearchParams<DeliveryAckRecord> parameters)
            => new(Array.Empty<DeliveryAckRecord>());

        public ValueTask MarkEventAsCompleteAsync(DeliveryAckRecord record, CancellationToken ct)
            => ValueTask.CompletedTask;

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<DeliveryAckRecord> parameters)
            => ValueTask.CompletedTask;
    }

    public sealed class DeliveryAckSubscriberState
    {
        public Lock Sync { get; } = new();
        public List<DeliveryAckRecord> Rows { get; } = [];
        public int StoreCalls;
        public int DuplicateCount;
        public int StoresWaiting;
        public Func<DeliveryAckRecord, ValueTask>? BeforeStore;
        public Action<DeliveryAckRecord>? AfterStore;
        public bool GateStores;
        public TimeSpan ClockSkewAllowance = TimeSpan.FromMinutes(5);
        public TaskCompletionSource ReleaseStores { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource EmptyFetch { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed class DeliveryAckSubscriberStorage(DeliveryAckSubscriberState state) : IEventSubscriberDeliveryAck<DeliveryAckRecord>
    {
        public TimeSpan DeliveryAckClockSkewAllowance => state.ClockSkewAllowance;
        public async ValueTask StoreEventAsync(DeliveryAckRecord record, CancellationToken ct)
        {
            if (state.BeforeStore is not null)
                await state.BeforeStore(record);

            if (state.GateStores)
            {
                Interlocked.Increment(ref state.StoresWaiting);
                await state.ReleaseStores.Task;
            }

            lock (state.Sync)
            {
                if (state.Rows.Exists(row => row.TrackingID == record.TrackingID))
                {
                    state.DuplicateCount++;

                    throw new DuplicateEventDeliveryException(record.TrackingID);
                }

                state.StoreCalls++;
                state.Rows.Add(DeliveryAckRecord.Clone(record));
            }

            state.AfterStore?.Invoke(record);
        }

        public ValueTask<IEnumerable<DeliveryAckRecord>> GetNextBatchAsync(PendingRecordSearchParams<DeliveryAckRecord> parameters)
        {
            var match = parameters.Match.Compile();

            lock (state.Sync)
            {
                var rows = state.Rows.Where(match).Take(parameters.Limit).Select(DeliveryAckRecord.Clone).ToList();

                if (rows.Count == 0)
                    state.EmptyFetch.TrySetResult();

                return new(rows);
            }
        }

        public ValueTask MarkEventAsCompleteAsync(DeliveryAckRecord record, CancellationToken ct)
        {
            lock (state.Sync)
            {
                var row = state.Rows.First(item => item.TrackingID == record.TrackingID);
                row.IsComplete = true;
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<DeliveryAckRecord> parameters)
        {
            var match = parameters.Match.Compile();
            lock (state.Sync)
                state.Rows.RemoveAll(row => match(row));

            return ValueTask.CompletedTask;
        }
    }

    public sealed class DeliveryAckPlainSubscriberStorage : IEventSubscriberStorageProvider<DeliveryAckRecord>
    {
        public ValueTask StoreEventAsync(DeliveryAckRecord record, CancellationToken ct)
            => ValueTask.CompletedTask;

        public ValueTask<IEnumerable<DeliveryAckRecord>> GetNextBatchAsync(PendingRecordSearchParams<DeliveryAckRecord> parameters)
            => new(Array.Empty<DeliveryAckRecord>());

        public ValueTask MarkEventAsCompleteAsync(DeliveryAckRecord record, CancellationToken ct)
            => ValueTask.CompletedTask;

        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<DeliveryAckRecord> parameters)
            => ValueTask.CompletedTask;
    }
}
