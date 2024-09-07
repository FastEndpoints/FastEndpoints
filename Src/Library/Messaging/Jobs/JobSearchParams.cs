using System.Linq.Expressions;

namespace FastEndpoints;

/// <summary>
/// a dto representing search parameters for job storage record
/// </summary>
/// <typeparam name="TStorageRecord">the type of storage record</typeparam>
public struct JobSearchParams<TStorageRecord> where TStorageRecord : IJobStorageRecord
{
    /// <summary>
    /// the ID of the specific job
    /// </summary>
    public Guid TrackingID { get; internal set; }

    /// <summary>
    /// a boolean lambda expression to match the next batch of records
    /// <code>
    /// 	r => r.QueueID == "xxx" &amp;&amp;
    /// 	     r.TrackingID == <see cref="TrackingID"/>
    /// </code>
    /// </summary>
    public Expression<Func<TStorageRecord, bool>> Match { get; internal set; }

    /// <summary>
    /// cancellation token
    /// </summary>
    public CancellationToken CancellationToken { get; internal set; }
}