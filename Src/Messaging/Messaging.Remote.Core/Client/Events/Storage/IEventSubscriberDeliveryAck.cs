namespace FastEndpoints;

/// <summary>
/// opt-in storage provider for a subscriber that acknowledges each delivery after it is stored.
/// <see cref="IEventStorageRecord.TrackingID" /> is assigned by the event hub and is the delivery key.
/// <see cref="IEventSubscriberStorageProvider{TStorageRecord}.StoreEventAsync" /> inserts that row.
/// a unique index on <see cref="IEventStorageRecord.TrackingID" /> is required.
/// on conflict, throw <see cref="DuplicateEventDeliveryException" /> and do not insert a second row.
/// the library signals the executor before acknowledging either a new row or a duplicate.
/// duplicates preserve the stored row's expiry, retention, and completion state.
/// records must implement <see cref="IEventDeliveryAckStorageRecord" /> and persist its RetainUntil field.
/// keep the row and unique key through that deadline even when IsComplete is true or ExpireOn has passed.
/// apply the supplied purge predicate, including retention, instead of independently deleting completed rows.
/// upgrade ACK hubs before subscribers so every delivery supplies a replay deadline.
/// </summary>
/// <typeparam name="TStorageRecord">the type of the storage record</typeparam>
public interface IEventSubscriberDeliveryAck<TStorageRecord> : IEventSubscriberStorageProvider<TStorageRecord>
    where TStorageRecord : class, IEventStorageRecord
{
    /// <summary>
    /// allowance for the subscriber clock being ahead of the hub clock and for in-flight replay delays.
    /// defaults to five minutes. configure a non-negative value covering the deployment's maximum difference and delay.
    /// </summary>
    TimeSpan DeliveryAckClockSkewAllowance => TimeSpan.FromMinutes(5);
}
