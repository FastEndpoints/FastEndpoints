namespace FastEndpoints;

/// <summary>
/// options for job queues
/// </summary>
public class JobQueueOptions
{
    //key: tCommand
    //val: handler execution time limit
    private readonly Dictionary<Type, (int concurrency, TimeSpan timeLimit)> _execLimits = new();

    /// <summary>
    /// specify execution limits such a max concurrency and execution time limit for each command type.
    /// </summary>
    /// <typeparam name="TCommand">the type of the command the limits apply to</typeparam>
    /// <param name="maxConcurrency">the maximum number of command executions of the same command type that's allowed to execute at the same time</param>
    /// <param name="timeLimit">
    /// the maximum amount of time each command is allowed to execute for.
    /// when execution time exceeds this value, a <see cref="OperationCanceledException"/> will be thrown.
    /// when that happens you can handle it in the <see cref="IJobStorageProvider{TStorageRecord}.OnHandlerExecutionFailureAsync(TStorageRecord, Exception, CancellationToken)"/> method.
    /// </param>
    public void ExecutionLimits<TCommand>(int maxConcurrency, TimeSpan timeLimit) where TCommand : ICommand
    {
        _execLimits[typeof(TCommand)] = new(maxConcurrency, timeLimit);
    }

    internal void SetExecutionLimits(Type tCommand, JobQueueBase jobQueue)
    {
        if (_execLimits.TryGetValue(tCommand, out var limits))
            jobQueue.SetExecutionLimits(limits.concurrency, limits.timeLimit);
    }
}