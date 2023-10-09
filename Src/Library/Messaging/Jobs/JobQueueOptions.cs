namespace FastEndpoints;

/// <summary>
/// options for job queues
/// </summary>
public class JobQueueOptions
{
    //key: tCommand
    //val: value tuple of concurrency and execution time limit
    readonly Dictionary<Type, (int concurrency, TimeSpan timeLimit)> _limitOverrides = new();

    /// <summary>
    /// the default max concurrency per job type. default value is the number of logical processors of the computer.
    /// you can specify per queue type overrides using <see cref="LimitsFor{TCommand}(int, TimeSpan)" />
    /// </summary>
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// the per job type max execution time limit for handler executions unless otherwise overridden using <see cref="LimitsFor{TCommand}(int, TimeSpan)" />
    /// defaults to <see cref="Timeout.Infinite" />.
    /// </summary>
    public TimeSpan ExecutionTimeLimit { get; set; } = Timeout.InfiniteTimeSpan;

    /// <summary>
    /// specify execution limits such a max concurrency and execution time limit for a given command type.
    /// </summary>
    /// <typeparam name="TCommand">the type of the command the limits apply to</typeparam>
    /// <param name="maxConcurrency">the maximum number of command executions of the same command type that's allowed to execute at the same time</param>
    /// <param name="timeLimit">
    /// the maximum amount of time each command is allowed to execute for.
    /// when execution time exceeds this value, a <see cref="OperationCanceledException" /> will be thrown.
    /// when that happens you can handle it in the
    /// <see cref="IJobStorageProvider{TStorageRecord}.OnHandlerExecutionFailureAsync(TStorageRecord, Exception, CancellationToken)" /> method.
    /// </param>
    public void LimitsFor<TCommand>(int maxConcurrency, TimeSpan timeLimit) where TCommand : ICommand
    {
        _limitOverrides[typeof(TCommand)] = new(maxConcurrency, timeLimit);
    }

    internal void SetExecutionLimits(Type tCommand, JobQueueBase jobQueue)
    {
        if (_limitOverrides.TryGetValue(tCommand, out var limits))
            jobQueue.SetExecutionLimits(limits.concurrency, limits.timeLimit);
        else
            jobQueue.SetExecutionLimits(MaxConcurrency, ExecutionTimeLimit);
    }
}