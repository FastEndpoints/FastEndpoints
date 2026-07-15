namespace FastEndpoints;

/// <summary>
/// thrown by a job storage provider when inserting a job would violate the
/// (<see cref="IJobStorageRecord.QueueID"/>, <see cref="IHasIdempotencyKey.IdempotencyKey"/>) uniqueness constraint.
/// the library catches this from <c>QueueJobAsync</c> and returns
/// <see cref="ExistingTrackingID"/> to the caller.
/// </summary>
public sealed class DuplicateJobException : Exception
{
    /// <summary>
    /// tracking id of the existing job that already owns the idempotency key.
    /// must not be <see cref="Guid.Empty"/>.
    /// </summary>
    public Guid ExistingTrackingID { get; }

    /// <summary>
    /// the conflicting idempotency key, if known.
    /// </summary>
    public string? IdempotencyKey { get; }

    /// <summary>
    /// the queue id of the conflicting job, if known.
    /// </summary>
    public string? QueueID { get; }

    /// <param name="existingTrackingId">tracking id of the already-stored job</param>
    /// <param name="idempotencyKey">the conflicting key</param>
    /// <param name="queueId">queue id of the conflicting job</param>
    /// <param name="message">optional exception message</param>
    /// <param name="inner">optional inner exception from the storage engine</param>
    public DuplicateJobException(Guid existingTrackingId,
                                 string? idempotencyKey = null,
                                 string? queueId = null,
                                 string? message = null,
                                 Exception? inner = null)
        : base(message ?? "A job with the same idempotency key already exists.", inner)
    {
        ExistingTrackingID = existingTrackingId;
        IdempotencyKey = idempotencyKey;
        QueueID = queueId;
    }
}
