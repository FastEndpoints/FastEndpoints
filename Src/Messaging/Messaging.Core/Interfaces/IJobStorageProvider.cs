namespace FastEndpoints;

/// <summary>
/// interface for defining the contract of a job storage provider
/// </summary>
/// <typeparam name="TStorageRecord">the type of job storage record of this storage provider</typeparam>
public interface IJobStorageProvider<TStorageRecord> where TStorageRecord : IJobStorageRecord
{
    /// <summary>
    /// store the job storage record however you please. ideally on a nosql database.
    /// </summary>
    /// <param name="r">the job storage record which contains the actual command object as well as some metadata</param>
    /// <param name="ct"></param>
    Task StoreJobAsync(TStorageRecord r, CancellationToken ct);

    /// <summary>
    /// fetch the next pending batch of job storage records that need to be processed, with the supplied search parameters.
    /// </summary>
    /// <param name="parameters">use these supplied search parameters to find the next batch of job records from your database</param>
    Task<IEnumerable<TStorageRecord>> GetNextBatchAsync(PendingJobSearchParams<TStorageRecord> parameters);

    /// <summary>
    /// mark the job storage record as complete by either replacing the entity on storage with the supplied instance or
    /// simply update the <see cref="IJobStorageRecord.IsComplete" /> field to true with a partial update operation.
    /// </summary>
    /// <param name="r">the job storage record to mark as complete</param>
    /// <param name="ct">cancellation token</param>
    Task MarkJobAsCompleteAsync(TStorageRecord r, CancellationToken ct);

    /// <summary>
    /// either delete the job storage record from the db using the supplied tracking id or update the <see cref="IJobStorageRecord.IsComplete" /> field to true
    /// with a partial update operation.
    /// </summary>
    /// <param name="trackingId">the <see cref="IJobStorageRecord.TrackingID" /> of the job to be cancelled</param>
    /// <param name="ct">cancellation token</param>
    Task CancelJobAsync(Guid trackingId, CancellationToken ct);

    /// <summary>
    /// this will only be triggered when a command handler (<see cref="ICommandHandler{TCommand}" />) associated with a command
    /// throws an exception. If you've set an execution time limit for the command, the thrown exception would be of type
    /// <see cref="OperationCanceledException" />.
    /// <para>
    /// when a job/command execution fails, it will be retried immediately. the failed job will be fetched again with the next batch of pending jobs.
    /// if one or more jobs keep failing repeatedly, it may cause the whole queue to get stuck in a retry loop preventing it from progressing.
    /// </para>
    /// <para>
    /// to prevent this from happening and allow other jobs to be given a chance at execution, you can reschedule failed jobs
    /// to be re-attempted at a future time instead. simply update the <see cref="IJobStorageRecord.ExecuteAfter" /> property to a future date/time
    /// and save the entity to the database (or do a partial update of only that property value).
    /// </para>
    /// </summary>
    /// <param name="r">the job that failed to execute successfully</param>
    /// <param name="exception">the exception that was thrown</param>
    /// <param name="ct">cancellation token</param>
    Task OnHandlerExecutionFailureAsync(TStorageRecord r, Exception exception, CancellationToken ct);

    /// <summary>
    /// this method will be called hourly. implement this method to delete stale records (completed or expired) from storage.
    /// you can safely delete the completed records. the incomplete &amp; expired records can be moved to some other location (dead-letter-queue maybe) or
    /// for inspection by a human.
    /// or if you'd like to retry expired events, update the <see cref="IJobStorageRecord.ExpireOn" /> field to a future date/time.
    /// </summary>
    /// <param name="parameters">use these supplied search parameters to find stale job records from your database</param>
    Task PurgeStaleJobsAsync(StaleJobSearchParams<TStorageRecord> parameters);
}

/// <summary>
/// addon interface to enable a job storage provider (<see cref="IJobStorageProvider{TStorageRecord}" />) to support commands that return results.
/// </summary>
public interface IJobResultProvider
{
    /// <summary>
    /// lookup the job storage record by the supplied tracking id and update it's <see cref="IJobResultStorage.Result" /> property and persist to the database.
    /// </summary>
    /// <param name="trackingId">the <see cref="IJobStorageRecord.TrackingID" /> of the job to be looked up</param>
    /// <param name="result">the job result to be stored</param>
    /// <param name="ct">cancellation token</param>
    /// <typeparam name="TResult">the type of the result object</typeparam>
    Task StoreJobResultAsync<TResult>(Guid trackingId, TResult result, CancellationToken ct);

    /// <summary>
    /// lookup the job storage record by the supplied tracking id and return its <see cref="IJobResultStorage.Result" /> value.
    /// </summary>
    /// <param name="trackingId">the <see cref="IJobStorageRecord.TrackingID" /> of the job to be looked up</param>
    /// <param name="ct">cancellation token</param>
    /// <typeparam name="TResult">the type of the result object</typeparam>
    Task<TResult?> GetJobResultAsync<TResult>(Guid trackingId, CancellationToken ct);
}