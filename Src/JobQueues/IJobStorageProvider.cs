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
    /// this method is used in single-instance (non-distributed) scenarios.
    /// <para>
    /// for distributed scenarios (multiple worker instances sharing the same data store), implement <see cref="IDistributedJobStorageProvider{TStorageRecord}" /> instead,
    /// which provides <see cref="IDistributedJobStorageProvider{TStorageRecord}.AtomicGetNextBatchAsync" /> for atomic claiming of job records.
    /// when the distributed interface is detected, this method will not be called by the job queue engine.
    /// </para>
    /// </summary>
    /// <param name="parameters">use these supplied search parameters to find the next batch of job records from your database</param>
    Task<ICollection<TStorageRecord>> GetNextBatchAsync(PendingJobSearchParams<TStorageRecord> parameters);

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
    /// <para>
    /// in distributed scenarios (when using <see cref="IDistributedJobStorageProvider{TStorageRecord}" />), you should also reset the
    /// <see cref="IJobStorageRecord.DequeueAfter" /> property (e.g., to <see cref="DateTime.MinValue" /> or to the same
    /// value as <see cref="IJobStorageRecord.ExecuteAfter" />) so that the job becomes eligible for any worker to pick up again.
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
/// addon interface for enabling distributed job processing across multiple worker instances sharing the same data store.
/// <para>
/// implement this interface on your job storage provider class (in addition to <see cref="IJobStorageProvider{TStorageRecord}" />) to enable distributed job processing.
/// when this interface is detected, the job queue engine will use <see cref="AtomicGetNextBatchAsync" /> instead of
/// <see cref="IJobStorageProvider{TStorageRecord}.GetNextBatchAsync" /> for fetching jobs, and <see cref="HasPendingJobsAsync" /> for determining whether the queue has any
/// pending work (including jobs scheduled for the future).
/// </para>
/// </summary>
/// <typeparam name="TStorageRecord">the type of job storage record</typeparam>
public interface IDistributedJobStorageProvider<TStorageRecord> where TStorageRecord : IJobStorageRecord
{
    /// <summary>
    /// atomically find and claim the next batch of pending job records that are ready for execution.
    /// <para>
    /// this method must use a database-level atomic operation (e.g., SQL <c>UPDATE...OUTPUT</c> with <c>READPAST</c>, PostgreSQL <c>UPDATE...RETURNING</c> with
    /// <c>FOR UPDATE SKIP LOCKED</c>, MongoDB <c>FindOneAndUpdate</c>, etc.) to find matching records and set <see cref="IJobStorageRecord.DequeueAfter" /> to a future
    /// date/time in a single atomic step. this prevents two workers from claiming the same job.
    /// </para>
    /// <para>
    /// the supplied <see cref="PendingJobSearchParams{TStorageRecord}.Match" /> expression includes a <see cref="IJobStorageRecord.DequeueAfter" /> <c>&lt;= now</c> check
    /// in addition to the standard eligibility filters (<c>QueueID</c>, <c>IsComplete</c>, <c>ExecuteAfter</c>, <c>ExpireOn</c>).
    /// providers that support LINQ expression translation (e.g., MongoDB) can use <c>p.Match</c> directly as the query filter.
    /// </para>
    /// <para>
    /// the <see cref="PendingJobSearchParams{TStorageRecord}.ExecutionTimeLimit" /> value from the search parameters can be used as a guide for determining the lease duration.
    /// </para>
    /// </summary>
    /// <param name="parameters">use these supplied search parameters to find the next batch of job records from your database</param>
    Task<ICollection<TStorageRecord>> AtomicGetNextBatchAsync(PendingJobSearchParams<TStorageRecord> parameters);

    /// <summary>
    /// check whether there are any pending (incomplete and non-expired) job records for the specified queue.
    /// this includes jobs scheduled for the future (where <see cref="IJobStorageRecord.ExecuteAfter" /> is in the future).
    /// <para>
    /// this is a lightweight existence check used by the job queue engine at startup to determine whether periodic polling should be enabled.
    /// it does not need to claim or return any records.
    /// </para>
    /// <para>
    /// the supplied <see cref="PendingJobSearchParams{TStorageRecord}.Match" /> expression matches records where:
    /// <c>QueueID == queueId &amp;&amp; !IsComplete &amp;&amp; ExpireOn &gt;= now</c>.
    /// note that <see cref="IJobStorageRecord.ExecuteAfter" /> is deliberately excluded so that future jobs are included.
    /// </para>
    /// </summary>
    /// <param name="parameters">use these supplied search parameters to check for pending job records in your database</param>
    /// <returns><c>true</c> if there are any pending jobs in the queue. otherwise <c>false</c></returns>
    Task<bool> HasPendingJobsAsync(PendingJobSearchParams<TStorageRecord> parameters);
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