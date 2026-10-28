namespace FastEndpoints;

/// <summary>
/// thrown by an opted-in subscriber storage provider when <see cref="IEventStorageRecord.TrackingID" /> was already stored.
/// the provider must not insert a second row. an empty <see cref="TrackingID" /> is a consumer bug: the library still
/// acknowledges the id from the wire message.
/// </summary>
public sealed class DuplicateEventDeliveryException : Exception
{
    /// <summary>
    /// the tracking id that conflicted. empty when the provider did not supply one.
    /// </summary>
    public Guid TrackingID { get; }

    /// <param name="trackingId">the tracking id that was already stored</param>
    /// <param name="message">optional exception message. the default includes <paramref name="trackingId" /></param>
    /// <param name="inner">optional inner exception from the storage engine</param>
    public DuplicateEventDeliveryException(Guid trackingId, string? message = null, Exception? inner = null)
        : base(message ?? $"Event delivery [{trackingId}] was already stored.", inner)
    {
        TrackingID = trackingId;
    }
}
