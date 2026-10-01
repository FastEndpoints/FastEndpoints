using System.Linq.Expressions;

namespace FastEndpoints;

static class EventSubscriberRetentionPolicy<TStorageRecord>
    where TStorageRecord : class, IEventStorageRecord
{
    internal static TimeSpan GetClockSkewAllowance(IEventSubscriberStorageProvider<TStorageRecord> storage)
    {
        if (!typeof(IEventDeliveryAckStorageRecord).IsAssignableFrom(typeof(TStorageRecord)))
            throw new InvalidOperationException("ACK inbox records must implement IEventDeliveryAckStorageRecord and persist RetainUntil.");

        var allowance = storage is IEventSubscriberDeliveryAck<TStorageRecord> ack
                            ? ack.DeliveryAckClockSkewAllowance
                            : TimeSpan.FromMinutes(5);

        if (allowance < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(IEventSubscriberDeliveryAck<>.DeliveryAckClockSkewAllowance));

        return allowance;
    }

    internal static DateTime GetRetainUntil(DateTime replayUntil, TimeSpan allowance)
        => replayUntil.Add(allowance);

    internal static Expression<Func<TStorageRecord, bool>> PurgeMatch
        => typeof(IEventDeliveryAckStorageRecord).IsAssignableFrom(typeof(TStorageRecord))
               ? r => (((IEventDeliveryAckStorageRecord)r).RetainUntil == null ||
                       DateTime.UtcNow > ((IEventDeliveryAckStorageRecord)r).RetainUntil) &&
                      (r.IsComplete || DateTime.UtcNow >= r.ExpireOn)
               : r => r.IsComplete || DateTime.UtcNow >= r.ExpireOn;
}
