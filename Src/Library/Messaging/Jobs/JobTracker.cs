namespace FastEndpoints;

/// <summary>
/// the interface defining a job tracker
/// </summary>
/// <typeparam name="TCommand">the command type of the job</typeparam>
public interface IJobTracker<TCommand> where TCommand : ICommand
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
    public Task CancelJobAsync(Guid trackingId, CancellationToken ct)
        => JobQueueWithoutReturnValue.CancelJobAsync<TCommand>(trackingId, ct);
}

/// <summary>
/// a <see cref="IJobTracker{TCommand}" /> implementation used for tracking queued jobs
/// </summary>
/// <typeparam name="TCommand">the command type of the job</typeparam>
public class JobTracker<TCommand> : IJobTracker<TCommand> where TCommand : ICommand
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
        => JobQueueWithoutReturnValue.CancelJobAsync<TCommand>(trackingId, ct);
}

/// <summary>
/// the interface defining a job tracker
/// </summary>
/// <typeparam name="TCommand">the command type of the job</typeparam>
/// <typeparam name="TResult">the result type of the job</typeparam>
public interface IJobTracker<TCommand, TResult> where TCommand : ICommand<TResult>
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
    public Task CancelJobAsync(Guid trackingId, CancellationToken ct)
        => JobQueueWithReturnValue<TResult>.CancelJobAsync<TCommand>(trackingId, ct);

    /// <summary>
    /// get mapped completed job result. the job storage record will be obtained via <see cref="IJobStorageProvider{TStorageRecord}.GetJob" /> method of the job storage
    /// provider, which require <see cref="IJobStorageRecord.IsComplete"/> to be true
    /// </summary>
    /// <param name="trackingId">the job tracking id</param>
    /// <param name="ct">optional cancellation token</param>
    /// <returns>nullable <typeparamref name="TResult"/></returns>
    /// <exception cref="Exception">
    /// this method will throw any exceptions that the job storage provider may throw in case of transient errors.
    /// </exception>
    public Task<TResult?> GetJobResultAsync(Guid trackingId, CancellationToken ct)
        => JobQueueWithReturnValue<TResult>.GetJobResultAsync<TCommand>(trackingId, ct);
}

/// <summary>
/// a <see cref="IJobTracker{TCommand,TResult}" /> implementation used for tracking queued jobs
/// </summary>
/// <typeparam name="TCommand">the command type of the job</typeparam>
/// <typeparam name="TResult">the result type of the job</typeparam>
public class JobTracker<TCommand, TResult> : IJobTracker<TCommand, TResult> where TCommand : ICommand<TResult>
{
    /// <inheritdoc />
    public static Task CancelJobAsync(Guid trackingId, CancellationToken ct = default)
        => JobQueueWithReturnValue<TResult>.CancelJobAsync<TCommand>(trackingId, ct);

    /// <inheritdoc />
    public Task<TResult?> GetJobResultAsync(Guid trackingId, CancellationToken ct)
        => JobQueueWithReturnValue<TResult>.GetJobResultAsync<TCommand>(trackingId, ct);
}