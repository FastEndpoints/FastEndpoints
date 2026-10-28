namespace FastEndpoints;

/// <summary>
/// opt-in storage provider that makes every event hub registered with this provider type expose <c>sub-ack</c> instead of <c>sub</c>.
/// <see cref="IEventHubStorageProvider{TStorageRecord}.MarkEventAsCompleteAsync" /> is the acknowledgement write.
/// there is no schema change. do not implement this on a dequeue-on-read store.
/// </summary>
/// <typeparam name="TStorageRecord">the type of the storage record</typeparam>
public interface IEventHubDeliveryAck<TStorageRecord> : IEventHubStorageProvider<TStorageRecord>
    where TStorageRecord : class, IEventStorageRecord
{
    /// <summary>
    /// maximum time for each delivery write and, separately, for its acknowledgement after the write completes.
    /// defaults to 30 seconds. each wait is capped by the event's remaining replay lifetime.
    /// must be positive and no greater than <c>TimeSpan.FromMilliseconds(uint.MaxValue - 1)</c>.
    /// increase this value for slow durable inbox writes. a timeout closes the subscription and leaves the hub row pending for replay.
    /// </summary>
    TimeSpan DeliveryAckTimeout => TimeSpan.FromSeconds(30);
}
