using System.Linq.Expressions;

namespace FastEndpoints;

/// <summary>
/// parameters to use in finding subscriber IDs to restore
/// </summary>
/// <typeparam name="TStorageRecord">the type of event storage record</typeparam>
public struct SubscriberIDRestorationParams<TStorageRecord> where TStorageRecord : IEventStorageRecord
{
    /// <summary>
    /// the type name of the events to search for which correlates to <see cref="IEventStorageRecord.EventType"/>
    /// </summary>
    public string EventType { get; internal set; }

    /// <summary>
    /// a boolean lambda expression to match pending records.
    /// <code>
    ///     r => r.EventType == "xxx" &amp;&amp; !r.IsComplete &amp;&amp; DateTime.UtcNow &lt;= r.ExpireOn)
    /// </code>
    /// </summary>
    public Expression<Func<TStorageRecord, bool>> Match { get; internal set; }

    /// <summary>
    /// member expression to select/project the UNIQUE <see cref="IEventStorageRecord.SubscriberID"/> values.
    /// <code>
    ///     e => e.SubscriberID
    /// </code>
    /// </summary>
    public Expression<Func<TStorageRecord, string>> Projection { get; internal set; }

    /// <summary>
    /// a cancellation token
    /// </summary>
    public CancellationToken CancellationToken { get; internal set; }
}