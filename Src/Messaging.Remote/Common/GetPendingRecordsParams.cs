using System.Linq.Expressions;

namespace FastEndpoints;

public struct GetPendingRecordsParams<TStorageRecord> where TStorageRecord : IEventStorageRecord
{
    /// <summary>
    /// the subscriber ID to fetch the next batch of pending records for
    /// </summary>
    public string SubscriberID { get; internal set; }

    /// <summary>
    /// a boolean lambda expression to match the next batch of pending records
    /// <code>
    ///   Where(e => e.SubscriberID == subscriberID &amp;&amp; !e.IsComplete &amp;&amp; DateTime.UtcNow &lt;= e.ExpireOn)
    /// </code>
    /// </summary>
    public Expression<Func<TStorageRecord, bool>> Match { get; internal set; }

    /// <summary>
    /// the number of pending records to fetch
    /// </summary>
    public int Limit { get; internal set; }

    /// <summary>
    /// cancellation token
    /// </summary>
    public CancellationToken CancellationToken { get; internal set; }
}