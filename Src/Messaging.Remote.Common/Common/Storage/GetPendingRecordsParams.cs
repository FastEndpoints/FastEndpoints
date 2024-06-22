using System.Linq.Expressions;

namespace FastEndpoints;

/// <summary>
/// a dto representing search parameters for pending event storage record retrieval
/// </summary>
/// <typeparam name="TStorageRecord">the type of storage record</typeparam>
public struct PendingRecordSearchParams<TStorageRecord> where TStorageRecord : IEventStorageRecord
{
    /// <summary>
    /// the subscriber ID for fetching the next batch of records
    /// </summary>
    public string SubscriberID { get; internal set; }

    /// <summary>
    /// a boolean lambda expression to match the next batch of records
    /// <code>
    ///     r => r.SubscriberID == "xxx" &amp;&amp;
    ///          !r.IsComplete &amp;&amp;
    ///          DateTime.UtcNow &lt;= r.ExpireOn
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