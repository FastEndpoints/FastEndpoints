using System.Linq.Expressions;

namespace FastEndpoints;

/// <summary>
/// a dto representing search parameters for matching stale event storage records
/// </summary>
/// <typeparam name="TStorageRecord">the type of storage record</typeparam>
public struct StaleRecordSearchParams<TStorageRecord> where TStorageRecord : IEventStorageRecord
{
    /// <summary>
    /// a boolean lambda expression to match stale records
    /// <code>
    ///     r => r.IsComplete || DateTime.UtcNow &gt;= r.ExpireOn
    /// </code>
    /// </summary>
    public Expression<Func<TStorageRecord, bool>> Match { get; internal set; }

    /// <summary>
    /// cancellation token
    /// </summary>
    public CancellationToken CancellationToken { get; internal set; }
}