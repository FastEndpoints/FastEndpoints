namespace FastEndpoints;

/// <summary>
/// options for job queues
/// </summary>
public class JobQueueOptions
{
    //key: tCommand
    //val: value tuple of concurrency and execution time limit
    readonly Dictionary<Type, (int concurrency, TimeSpan timeLimit)> _limitOverrides = new();

    //key: tCommand
    //val: extracts a formatted idempotency key from a command instance (null/empty = no key)
    readonly Dictionary<Type, Func<object, string?>> _idempotencyExtractors = new();

    /// <summary>
    /// the default max concurrency per job type. default value is the number of logical processors of the computer.
    /// you can specify per queue type overrides using <see cref="LimitsFor{TCommand}(int, TimeSpan)" />
    /// </summary>
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// specifies the interval for periodic re-checks of the storage to detect any scheduled jobs. these checks ensure that re-scheduled jobs are promptly executed.
    /// the default interval is set to 60 seconds. a shorter delay will make re-scheduled jobs run faster but will increase the overall load on the storage system,
    /// due to too frequent queries being issued. only reduce this delay if you need re-scheduled jobs re-execute faster.
    /// </summary>
    public TimeSpan StorageProbeDelay { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// specifies the delay between retries when transient storage operations fail while polling, persisting results, or marking completion.
    /// the default is 5 seconds. lower values reduce recovery latency but increase pressure on the backing store.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

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
    public void LimitsFor<TCommand>(int maxConcurrency, TimeSpan timeLimit) where TCommand : ICommandBase
    {
        _limitOverrides[typeof(TCommand)] = new(maxConcurrency, timeLimit);
    }

    /// <summary>
    /// enable storage-enforced idempotency for a command type using a function that returns the business key string.
    /// when the same non-empty key is queued again for that command type (same <see cref="IJobStorageRecord.QueueID"/>),
    /// the duplicate is discarded and the existing job's tracking id is returned.
    /// <para>
    /// null/empty/whitespace return values are treated as "no key" and are not deduped.
    /// format non-string values yourself (e.g. <c>c =&gt; c.PaymentId.ToString("D")</c>).
    /// </para>
    /// <para>
    /// requires the storage record to implement <see cref="IHasIdempotencyKey"/> and the storage provider to enforce a unique
    /// index on (<see cref="IJobStorageRecord.QueueID"/>, <see cref="IHasIdempotencyKey.IdempotencyKey"/>) while the row exists.
    /// </para>
    /// </summary>
    /// <typeparam name="TCommand">command type to make idempotent</typeparam>
    /// <param name="keySelector">function that extracts the business key string from the command</param>
    public void IdempotencyKeyFor<TCommand>(Func<TCommand, string?> keySelector) where TCommand : class, ICommandBase
    {
        ArgumentNullException.ThrowIfNull(keySelector);

        _idempotencyExtractors[typeof(TCommand)] = cmd =>
        {
            var key = keySelector((TCommand)cmd);

            return string.IsNullOrWhiteSpace(key) ? null : key;
        };
    }

    internal bool HasAnyIdempotencyConfig
        => _idempotencyExtractors.Count > 0;

    internal bool TryGetIdempotencyExtractor(Type tCommand, out Func<object, string?> extractor)
        => _idempotencyExtractors.TryGetValue(tCommand, out extractor!);

    internal bool WarmupRequested { get; private set; }

    /// <summary>
    /// pre-initialize event bus instances during startup.
    /// messaging warmup is lazy by default and only runs when this method is called from <c>UseJobQueues(...)</c>.
    /// </summary>
    public void Warmup()
        => WarmupRequested = true;

    internal void SetLimits(Type tCommand, JobQueueBase jobQueue)
    {
        if (_limitOverrides.TryGetValue(tCommand, out var limits))
            jobQueue.SetLimits(limits.concurrency, limits.timeLimit, StorageProbeDelay, RetryDelay);
        else
            jobQueue.SetLimits(MaxConcurrency, ExecutionTimeLimit, StorageProbeDelay, RetryDelay);

        if (TryGetIdempotencyExtractor(tCommand, out var extractor))
            jobQueue.SetIdempotencyKeyExtractor(extractor);
    }
}
