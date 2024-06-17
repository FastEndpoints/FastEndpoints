namespace FastEndpoints;

/// <summary>
/// a static class used for tracking queued jobs
/// </summary>
/// <typeparam name="TCommand">the command type of the job</typeparam>
public static class JobTracker<TCommand> where TCommand : ICommand
{
    /// <summary>
    /// cancel a job by its tracking id. if the job is currently executing, the cancellation token passed down to the command handler method will be notified of the
    /// cancellation. the job storage record will also be removed/marked complete via <see cref="IJobStorageProvider{TStorageRecord}.CancelJobAsync" /> method of the job storage
    /// provider.
    /// </summary>
    /// <param name="trackingId">the job tracking id</param>
    /// <param name="ct">optional cancellation token</param>
    public static Task CancelJobAsync(Guid trackingId, CancellationToken ct = default)
        => JobQueueBase.CancelJobAsync<TCommand>(trackingId, ct);
}