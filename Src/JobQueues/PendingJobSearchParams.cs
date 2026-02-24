using System.Linq.Expressions;

namespace FastEndpoints;

/// <summary>
/// a dto representing search parameters for pending job storage record retrieval
/// </summary>
/// <typeparam name="TStorageRecord">the type of storage record</typeparam>
public struct PendingJobSearchParams<TStorageRecord> where TStorageRecord : IJobStorageRecord
{
    /// <summary>
    /// the ID of the job queue for fetching the next batch of records for.
    /// </summary>
    public string QueueID { get; internal set; }

    /// <summary>
    /// a boolean lambda expression to match the next batch of records.
    /// <code>
    /// 	r => r.QueueID == "xxx" &amp;&amp;
    /// 	     !r.IsComplete &amp;&amp;
    /// 	     r.ExecuteAfter &lt;= now &amp;&amp;
    /// 	     r.ExpireOn &gt;= now &amp;&amp;
    /// 	     r.DequeueAfter &lt;= now
    /// </code>
    /// <para>
    /// note: the <see cref="IJobStorageRecord.DequeueAfter" /> <c>&lt;= now</c> check is always included. for non-distributed (single-instance) providers,
    /// this condition is always true since <see cref="IJobStorageRecord.DequeueAfter" /> defaults to <see cref="DateTime.MinValue" />.
    /// </para>
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

    /// <summary>
    /// the execution time limit configured for the job queue type these records belong to.
    /// in distributed scenarios, this value can be used by the storage provider to determine a suitable lease duration when atomically claiming job records.
    /// i.e. set the <see cref="IJobStorageRecord.DequeueAfter" /> property to <c>DateTime.UtcNow + ExecutionTimeLimit</c> when picking up jobs.
    /// a value of <see cref="Timeout.InfiniteTimeSpan" /> indicates no specific time limit has been configured.
    /// </summary>
    public TimeSpan ExecutionTimeLimit { get; internal set; }
}