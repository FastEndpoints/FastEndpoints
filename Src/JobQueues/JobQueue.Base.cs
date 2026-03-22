using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace FastEndpoints;

[UnconditionalSuppressMessage("aot", "IL2090")]
abstract class JobQueueBase
{
    //key: tCommand
    //val: job queue for the command type
    //values get created when the DI container resolves each job queue type and the ctor is run.
    //see ctor in JobQueue<TCommand, TStorageRecord, TStorageProvider>
    protected static readonly ConcurrentDictionary<Type, JobQueueBase> JobQueues = new();

    protected abstract IJobStorageRecord CreateJob(ICommandBase command, DateTime? executeAfter, DateTime? expireOn);

    protected abstract void TriggerJob();

    protected abstract Task<Guid> StoreJobAsync(ICommandBase command, DateTime? executeAfter, DateTime? expireOn, CancellationToken ct);

    protected abstract Task CancelJobAsync(Guid trackingId, CancellationToken ct);

    protected abstract Task<TResult?> GetJobResultAsync<TResult>(Guid trackingId, CancellationToken ct);

    protected abstract Task StoreJobResultAsync<TResult>(Guid trackingId, TResult result, CancellationToken ct);

    internal abstract void SetLimits(int concurrencyLimit, TimeSpan executionTimeLimit, TimeSpan semWaitLimit, TimeSpan? retryDelay = null);

    static JobQueueBase GetQueue(Type tCommand)
        => JobQueues.TryGetValue(tCommand, out var queue)
               ? queue
               : throw new InvalidOperationException($"A job queue has not been registered for [{tCommand.FullName}]");

    static void ValidateResultSupport(Type tCommand)
    {
        var tResult = tCommand.GetInterface(typeof(ICommand<>).Name)?.GetGenericArguments()[0];

        if (tResult == Types.VoidResult)
            throw new InvalidOperationException($"Job results are not supported with commands that don't return a result! Offending command: [{tCommand.FullName}]");
    }

    internal static TStorageRecord CreateJob<TStorageRecord>(ICommandBase command, DateTime? executeAfter, DateTime? expireOn)
        where TStorageRecord : class, IJobStorageRecord, new()
        => (TStorageRecord)GetQueue(command.GetType()).CreateJob(command, executeAfter, expireOn);

    internal static void TriggerJobExecution(Type commandType)
        => GetQueue(commandType).TriggerJob();

    internal static Task<Guid> AddToQueueAsync(ICommandBase command, DateTime? executeAfter, DateTime? expireOn, CancellationToken ct)
        => GetQueue(command.GetType()).StoreJobAsync(command, executeAfter, expireOn, ct);

    internal static Task CancelJobAsync<TCommand>(Guid trackingId, CancellationToken ct) where TCommand : ICommandBase
        => GetQueue(typeof(TCommand)).CancelJobAsync(trackingId, ct);

    internal static Task<TResult?> GetJobResultAsync<TCommand, TResult>(Guid trackingId, CancellationToken ct) where TCommand : ICommandBase
    {
        var tCommand = typeof(TCommand);
        ValidateResultSupport(tCommand);

        return GetQueue(tCommand).GetJobResultAsync<TResult>(trackingId, ct);
    }

    internal static Task StoreJobResultAsync<TCommand, TResult>(Guid trackingId, TResult result, CancellationToken ct)
        where TCommand : ICommandBase
        where TResult : IJobResult
    {
        var tCommand = typeof(TCommand);
        ValidateResultSupport(tCommand);

        return GetQueue(tCommand).StoreJobResultAsync(trackingId, result, ct);
    }
}