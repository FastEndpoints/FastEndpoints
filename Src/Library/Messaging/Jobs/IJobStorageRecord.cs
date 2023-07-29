namespace FastEndpoints;

public interface IJobStorageRecord
{
    /// <summary>
    /// a unique id for the job queue. each command type has it's own queue.
    /// </summary>
    string QueueID { get; set; }

    /// <summary>
    /// the actual command object that will be embedded in the storage record.
    /// if your database doesn't support embedding objects, you may have to serialize the object and store it in this property.
    /// </summary>
    object Command { get; set; }

    /// <summary>
    /// the job will not be executed before this date/time. by default it will automatically be set to the time of creation allowing jobs to be
    /// executed as soon as they're created.
    /// </summary>
    DateTime ExecuteAfter { get; set; }

    /// <summary>
    /// the expiration date/time of job. if the job remains in an incomplete state past this time, the record is considered stale.
    /// </summary>
    DateTime ExpireOn { get; set; }

    /// <summary>
    /// indicates whether the job has successfully completed or not.
    /// </summary>
    bool IsComplete { get; set; }
}