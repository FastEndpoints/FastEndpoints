// ReSharper disable UnusedParameter.Global

namespace FastEndpoints;

/// <summary>
/// interface for implementing a storage provider for an event subscription client app (gRPC client)
/// </summary>
/// <typeparam name="TStorageRecord">the type of the storage record</typeparam>
public interface IEventSubscriberStorageProvider<TStorageRecord> where TStorageRecord : IEventStorageRecord
{
    /// <summary>
    /// store the event storage record however you please. ideally on a nosql database.
    /// </summary>
    /// <param name="r">the event storage record which contains the actual event object as well as some metadata</param>
    /// <param name="ct">cancellation token</param>
    ValueTask StoreEventAsync(TStorageRecord r, CancellationToken ct);

    /// <summary>
    /// fetch the next batch of pending event storage records that need to be processed.
    /// </summary>
    /// <param name="parameters">use these supplied search parameters to find the next batch of event records from your database</param>
    ValueTask<IEnumerable<TStorageRecord>> GetNextBatchAsync(PendingRecordSearchParams<TStorageRecord> parameters);

    /// <summary>
    /// mark the event storage record as complete by either replacing the entity on storage with the supplied instance or
    /// simply update the <see cref="IEventStorageRecord.IsComplete" /> field to true with a partial update operation.
    /// </summary>
    /// <param name="r">the storage record to mark complete</param>
    /// <param name="ct">cancellation token</param>
    ValueTask MarkEventAsCompleteAsync(TStorageRecord r, CancellationToken ct);

    /// <summary>
    /// this method will be called hourly. implement this method to remove stale records (completed or expired) from storage.
    /// or instead of removing them, you can move them to some other location (dead-letter-queue maybe) or for inspection by a human.
    /// or if you'd like to retry expired events, update the <see cref="IEventStorageRecord.ExpireOn" /> field to a future date/time.
    /// <para>
    /// NOTE: the default match criteria is:
    /// <code>
    ///     r => r.IsComplete || DateTime.UtcNow &gt;= r.ExpireOn
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="parameters">use these supplied search parameters to find stale records</param>
    ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<TStorageRecord> parameters);
}