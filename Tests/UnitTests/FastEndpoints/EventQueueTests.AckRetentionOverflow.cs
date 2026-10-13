using FastEndpoints;
using Shouldly;
using Xunit;
using static QueueTesting.QueueTestSupport;

namespace EventQueue;

public partial class EventQueueTests
{
    [Fact]
    public void delivery_ack_retention_addition_saturates_at_utc_maximum()
    {
        var maximum = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
        var ordinary = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

        foreach (var (deadline, allowance) in new[]
                 {
                     (DateTime.MaxValue, TimeSpan.FromMinutes(5)),
                     (ordinary, TimeSpan.MaxValue),
                     (maximum.AddTicks(-1), TimeSpan.FromTicks(2)),
                     (maximum.AddTicks(-1), TimeSpan.FromTicks(1)),
                     (maximum, TimeSpan.Zero)
                 })
        {
            var retainUntil = EventSubscriberRetentionPolicy<DeliveryAckRecord>.GetRetainUntil(deadline, allowance);
            retainUntil.ShouldBe(maximum);
            retainUntil.Kind.ShouldBe(DateTimeKind.Utc);
        }

        EventSubscriberRetentionPolicy<DeliveryAckRecord>.GetRetainUntil(ordinary, TimeSpan.Zero).ShouldBe(ordinary);
        EventSubscriberRetentionPolicy<DeliveryAckRecord>.GetRetainUntil(ordinary, TimeSpan.FromMinutes(5))
                                                         .ShouldBe(ordinary.AddMinutes(5));
    }

    [Fact]
    public Task delivery_ack_maximum_expiry_accepts_duplicate_and_drains_next_backlog_row()
        => VerifyOverflowingRetention<DeliveryAckMaximumExpiryEvent>(DateTime.MaxValue, TimeSpan.FromMinutes(5));

    [Fact]
    public Task delivery_ack_maximum_allowance_accepts_duplicate_and_drains_next_backlog_row()
        => VerifyOverflowingRetention<DeliveryAckMaximumAllowanceEvent>(DateTime.UtcNow.AddHours(4), TimeSpan.MaxValue);

    static async Task VerifyOverflowingRetention<TEvent>(DateTime replayUntil, TimeSpan allowance)
        where TEvent : class, IEvent, new()
    {
        await using var session = DeliveryAckSession<TEvent>.Create();
        const string subscriberId = "retention-overflow-sub";
        session.SubState.ClockSkewAllowance = allowance;
        session.Bridge.DropAckCalls = 1;
        var firstId = Guid.NewGuid();
        var nextId = Guid.NewGuid();
        var nextDeadline = DateTime.UtcNow.AddHours(1);
        session.HubState.Rows.Add(new()
        {
            SubscriberID = subscriberId,
            TrackingID = firstId,
            EventType = typeof(TEvent).FullName!,
            Event = new TEvent(),
            ExpireOn = replayUntil
        });
        session.HubState.Rows.Add(new()
        {
            SubscriberID = subscriberId,
            TrackingID = nextId,
            EventType = typeof(TEvent).FullName!,
            Event = new TEvent(),
            ExpireOn = nextDeadline
        });

        await session.Connect(subscriberId);
        await WaitUntil(() => session.HubState.Rows.All(row => row.IsComplete) &&
                              DeliveryAckCountingHandler<TEvent>.Runs == 2);

        session.SubState.StoreCalls.ShouldBe(2);
        session.SubState.DuplicateCount.ShouldBe(1);
        session.SubState.Rows.Count.ShouldBe(2);
        var first = session.SubState.Rows.Single(row => row.TrackingID == firstId);
        first.RetainUntil.ShouldBe(DateTime.MaxValue);
        first.RetainUntil!.Value.Kind.ShouldBe(DateTimeKind.Utc);
        EventSubscriberRetentionPolicy<DeliveryAckRecord>.PurgeMatch.Compile()(first).ShouldBeFalse();
        var next = session.SubState.Rows.Single(row => row.TrackingID == nextId);
        next.RetainUntil.ShouldBe(allowance == TimeSpan.MaxValue ? DateTime.MaxValue : nextDeadline.Add(allowance));
    }

    public sealed class DeliveryAckMaximumExpiryEvent : IEvent;
    public sealed class DeliveryAckMaximumAllowanceEvent : IEvent;
}
