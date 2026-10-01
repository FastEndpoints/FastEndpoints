using FastEndpoints;
using Xunit;
using static QueueTesting.QueueTestSupport;

namespace EventQueue;

public partial class EventQueueTests
{
    [Fact]
    public async Task delivery_ack_store_retries_refresh_execution_expiry()
    {
        await using var session = DeliveryAckSession<DeliveryAckStoreRetryEvent>.Create();
        var attempts = 0;
        var originalExpiry = default(DateTime);
        var successfulAttempt = default(DateTime);
        session.SubState.BeforeStore = async record =>
        {
            if (++attempts == 1)
            {
                originalExpiry = record.ExpireOn;
                throw new IOException("inbox unavailable");
            }

            if (attempts == 2)
            {
                await WaitUntil(() => DateTime.UtcNow > originalExpiry);
                throw new IOException("inbox still unavailable");
            }

            successfulAttempt = DateTime.UtcNow;
        };
        await session.Connect("retry-expiry", executionExpiry: TimeSpan.FromSeconds(1));
        await session.Hub.BroadcastEventTaskForTesting(new());
        await WaitUntil(() => session.HubState.Rows.Single().IsComplete);

        var persisted = session.SubState.Rows.Single();
        persisted.ExpireOn.ShouldBeGreaterThan(originalExpiry);
        persisted.ExpireOn.ShouldBeGreaterThan(successfulAttempt.AddMilliseconds(900));
        persisted.RetainUntil.ShouldBe(session.HubState.Rows.Single().ExpireOn.Add(session.SubState.ClockSkewAllowance));
        await WaitUntil(() => DeliveryAckCountingHandler<DeliveryAckStoreRetryEvent>.Runs == 1 && persisted.IsComplete);
        attempts.ShouldBe(3);
        session.SubState.StoreCalls.ShouldBe(1);
    }

    [Fact]
    public async Task delivery_ack_store_commit_then_error_preserves_duplicate_metadata()
    {
        await using var session = DeliveryAckSession<DeliveryAckStoreCommitErrorEvent>.Create();
        DeliveryAckRecord? committed = null;
        session.SubState.AfterStore = record =>
        {
            committed = DeliveryAckRecord.Clone(record);
            lock (session.SubState.Sync)
                session.SubState.Rows.Single().IsComplete = true;
            throw new IOException("commit response lost");
        };
        await session.Connect("commit-error");
        await session.Hub.BroadcastEventTaskForTesting(new());
        await WaitUntil(() => session.HubState.Rows.Single().IsComplete);

        var persisted = session.SubState.Rows.Single();
        committed.ShouldNotBeNull();
        persisted.ExpireOn.ShouldBe(committed.ExpireOn);
        persisted.RetainUntil.ShouldBe(committed.RetainUntil);
        persisted.IsComplete.ShouldBeTrue();
        session.SubState.StoreCalls.ShouldBe(1);
        session.SubState.DuplicateCount.ShouldBe(1);
        DeliveryAckCountingHandler<DeliveryAckStoreCommitErrorEvent>.Runs.ShouldBe(0);
    }

    [Fact]
    public async Task delivery_ack_store_retry_cancellation_keeps_hub_delivery_pending()
    {
        await using var session = DeliveryAckSession<DeliveryAckStoreCancelEvent>.Create();
        var attempts = 0;
        session.SubState.BeforeStore = _ =>
        {
            Interlocked.Increment(ref attempts);
            throw new IOException("inbox unavailable");
        };
        await session.Connect("cancel-retry");
        await session.Hub.BroadcastEventTaskForTesting(new());
        await WaitUntil(() => Volatile.Read(ref attempts) >= 2);
        session.Cts.Cancel();
        await WaitUntil(() => session.ConnectionCount("cancel-retry") == 0);

        session.HubState.Rows.Single().IsComplete.ShouldBeFalse();
        session.HubState.MarkCalls.ShouldBe(0);
        session.SubState.Rows.ShouldBeEmpty();
        DeliveryAckCountingHandler<DeliveryAckStoreCancelEvent>.Runs.ShouldBe(0);
    }

    [Fact]
    public async Task delivery_ack_successful_store_longer_than_execution_window_remains_expired()
    {
        await using var session = DeliveryAckSession<DeliveryAckStoreBoundaryEvent>.Create();
        session.SubState.BeforeStore = async record =>
            await WaitUntil(() => DateTime.UtcNow > record.ExpireOn);
        await session.Connect("expiry-boundary", executionExpiry: TimeSpan.FromMilliseconds(200));
        await session.Hub.BroadcastEventTaskForTesting(new());
        await WaitUntil(() => session.HubState.Rows.Single().IsComplete);
        await session.SubState.EmptyFetch.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var persisted = session.SubState.Rows.Single();
        persisted.ExpireOn.ShouldBeLessThan(DateTime.UtcNow);
        persisted.IsComplete.ShouldBeFalse();
        persisted.RetainUntil.ShouldBe(session.HubState.Rows.Single().ExpireOn.Add(session.SubState.ClockSkewAllowance));
        DeliveryAckCountingHandler<DeliveryAckStoreBoundaryEvent>.Runs.ShouldBe(0);
    }

    public sealed class DeliveryAckStoreRetryEvent : IEvent;
    public sealed class DeliveryAckStoreCommitErrorEvent : IEvent;
    public sealed class DeliveryAckStoreCancelEvent : IEvent;
    public sealed class DeliveryAckStoreBoundaryEvent : IEvent;
}
