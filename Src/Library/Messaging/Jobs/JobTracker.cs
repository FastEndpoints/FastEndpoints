namespace FastEndpoints;

/// <summary>
/// the interface defining a job tracker
/// </summary>
/// <typeparam name="TCommand">the command type of the job</typeparam>
public interface IJobTracker<TCommand> where TCommand : ICommandBase
{
    /// <summary>
    /// cancel a job by its tracking id. if the job is currently executing, the cancellation token passed down to the command handler method will be notified of the
    /// cancellation. the job storage record will also be marked complete via <see cref="IJobStorageProvider{TStorageRecord}.CancelJobAsync" /> method of the job storage
    /// provider, which will prevent the job from being picked up for execution.
    /// </summary>
    /// <param name="trackingId">the job tracking id</param>
    /// <param name="ct">optional cancellation token</param>
    /// <exception cref="Exception">
    /// this method will throw any exceptions that the job storage provider may throw in case of transient errors. you can safely retry calling this
    /// method repeatedly with the same tracking id.
    /// </exception>
    public Task CancelJobAsync(Guid trackingId, CancellationToken ct = default)
        => JobQueueBase.CancelJobAsync<TCommand>(trackingId, ct);

    /// <summary>
    /// this method can be used to either store a preliminary job result and/or job progress before the job execution fully completes.
    /// </summary>
    /// <param name="trackingId">the job tracking id</param>
    /// <param name="result">the preliminary job result or progress to store</param>
    /// <param name="ct">cancellation token</param>
    /// <typeparam name="TResult">the type of the preliminary result or progress</typeparam>
    public Task StoreJobResultAsync<TResult>(Guid trackingId, TResult result, CancellationToken ct = default) where TResult : IJobResult
        => JobQueueBase.StoreJobResultAsync<TCommand, TResult>(trackingId, result, ct);

    /// <summary>
    /// retrieve the result of a command (that returns a result) which was previously queued as a job.
    /// the returned result will be null/default until the job is actually complete.
    /// </summary>
    /// <param name="trackingId">the job tracking id</param>
    /// <param name="ct">cancellation token</param>
    /// <typeparam name="TResult">the type of the expected result</typeparam>
    public Task<TResult?> GetJobResultAsync<TResult>(Guid trackingId, CancellationToken ct = default)
        => JobQueueBase.GetJobResultAsync<TCommand, TResult>(trackingId, ct);
}

/// <summary>
/// a <see cref="IJobTracker{TCommand}" /> implementation used for tracking queued jobs
/// </summary>
/// <typeparam name="TCommand">the command type of the job</typeparam>
public class JobTracker<TCommand> : IJobTracker<TCommand> where TCommand : ICommandBase
{
    /// <summary>
    /// cancel a job by its tracking id. if the job is currently executing, the cancellation token passed down to the command handler method will be notified of the
    /// cancellation. the job storage record will also be marked complete via <see cref="IJobStorageProvider{TStorageRecord}.CancelJobAsync" /> method of the job storage
    /// provider, which will prevent the job from being picked up for execution.
    /// </summary>
    /// <param name="trackingId">the job tracking id</param>
    /// <param name="ct">optional cancellation token</param>
    /// <exception cref="Exception">
    /// this method will throw any exceptions that the job storage provider may throw in case of transient errors. you can safely retry calling this
    /// method repeatedly with the same tracking id.
    /// </exception>
    public static Task CancelJobAsync(Guid trackingId, CancellationToken ct = default)
        => JobQueueBase.CancelJobAsync<TCommand>(trackingId, ct);

    /// <summary>
    /// this method can be used to either store a preliminary job result and/or job progress before the job execution fully completes.
    /// </summary>
    /// <param name="trackingId">the job tracking id</param>
    /// <param name="result">the preliminary job result or progress to store</param>
    /// <param name="ct">cancellation token</param>
    /// <typeparam name="TResult">the type of the preliminary result or progress</typeparam>
    public static Task StoreJobResultAsync<TResult>(Guid trackingId, TResult result, CancellationToken ct = default) where TResult : IJobResult
        => JobQueueBase.StoreJobResultAsync<TCommand, TResult>(trackingId, result, ct);

    /// <summary>
    /// retrieve the result of a command (that returns a result) which was previously queued as a job.
    /// the returned result will be null/default until the job is actually complete.
    /// </summary>
    /// <param name="trackingId">the job tracking id</param>
    /// <param name="ct">cancellation token</param>
    /// <typeparam name="TResult">the type of the expected result</typeparam>
    public static Task<TResult?> GetJobResultAsync<TResult>(Guid trackingId, CancellationToken ct = default)
        => JobQueueBase.GetJobResultAsync<TCommand, TResult>(trackingId, ct);
}