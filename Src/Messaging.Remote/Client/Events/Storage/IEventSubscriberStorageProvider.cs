namespace FastEndpoints;

/// <summary>
/// interface for implementing a storage provider for event subscription client app (gRPC client)
/// </summary>
/// <typeparam name="TStorageRecord">the type of the storage record</typeparam> 
public interface IEventSubscriberStorageProvider<TStorageRecord> where TStorageRecord : IEventStorageRecord
{
    /// <summary>
    /// store the event storage record however you please. ideally on a nosql database.
    /// </summary>
    /// <param name="e">the event storage record which contains the actual event object as well as some metadata</param>
    /// <param name="ct">cancellation token</param>
    ValueTask StoreEventAsync(TStorageRecord e, CancellationToken ct);

    /// <summary>
    /// fetch the next batch of pending event storage records that need to be processed.
    /// </summary>
    ValueTask<IEnumerable<TStorageRecord>> GetNextBatchAsync(GetPendingRecordsParams<TStorageRecord> parameters);

    /// <summary>
    /// mark the event storage record as complete by either replacing the entity on storage with the supplied instance or
    /// simply update the <see cref="IEventStorageRecord.IsComplete"/> field to true with a partial update operation.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="ct">cancellation token</param>
    ValueTask MarkEventAsCompleteAsync(TStorageRecord e, CancellationToken ct);

    /// <summary>
    /// this method will be called hourly. implement this method to remove stale records (completed or (expired and incomplete)) from storage.
    /// or instead of removing them, you can move them to some other location (dead-letter-queue maybe) or for inspection by a human.
    /// or if you'd like to retry expired events, update the <see cref="IEventStorageRecord.ExpireOn"/> field to a future date/time.
    /// </summary>
    ValueTask PurgeStaleRecordsAsync();
}