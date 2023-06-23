namespace FastEndpoints;

/// <summary>
/// interface for implementing a storage provider for event publisher app (gRPC server)
/// </summary>
public interface IEventPublisherStorageProvider
{
    /// <summary>
    /// this method will only be called once (for each event type) on app startup. if there are any pending records on storage from a previous app run,
    /// simply return a collection of unique subscriber IDs.
    /// <code>
    ///     Where(e => e.EventType == eventType &amp;&amp; !e.IsComplete &amp;&amp; DateTime.UtcNow &lt;= e.ExpireOn)
    ///     Select(e => e.SubscriberID)
    ///     Distinct()
    /// </code>
    /// </summary>
    /// <param name="eventType">the full type name of the event model</param>
    ValueTask<IEnumerable<string>> RestoreSubsriberIDsForEventType(string eventType);

    /// <summary>
    /// store the event storage record however you please. ideally on a nosql database.
    /// </summary>
    /// <param name="e">the event storage record which contains the actual event object as well as some metadata</param>
    /// <param name="ct">cancellation token</param>
    ValueTask StoreEventAsync(IEventStorageRecord e, CancellationToken ct);

    /// <summary>
    /// fetch the next pending event storage record that needs to be processed.
    /// <code>
    ///   Where(e => e.SubscriberID == subscriberID &amp;&amp; !e.IsComplete &amp;&amp; DateTime.UtcNow &lt;= e.ExpireOn)
    ///   OrderDescending(e => e.Id)
    ///   Take(1)
    /// </code>
    /// </summary>
    /// <param name="subscriberID">the id of the subscriber who's next event that should be retrieved</param>
    /// <param name="ct">cancellation token</param>
    ValueTask<IEventStorageRecord?> GetNextEventAsync(string subscriberID, CancellationToken ct);

    /// <summary>
    /// mark the event storage record as complete by either replacing the entity on storage with the supplied instance or
    /// simply update the <see cref="IEventStorageRecord.IsComplete"/> field to true with a partial update operation.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="ct">cancellation token</param>
    ValueTask MarkEventAsCompleteAsync(IEventStorageRecord e, CancellationToken ct);

    /// <summary>
    /// this method will be called hourly. implement this method to remove both expired and incomplete records from storage.
    /// or instead of removing them, you can move them to some other location (dead-letter-queue maybe) or for inspection by a human.
    /// or if you'd like to retry expired events, update the <see cref="IEventStorageRecord.ExpireOn"/> field to a future date/time.
    /// </summary>
    ValueTask PurgeStaleRecordsAsync();
}
