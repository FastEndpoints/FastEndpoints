using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace FastEndpoints;

public abstract class JobQueueBase
{
    //key: tCommand
    //val: job queue for the command type
    //values get created when the DI container resolves each job queue type and the ctor is run.
    //see ctor in JobQueue<TCommand, TStorageRecord, TStorageProvider>
    protected static readonly ConcurrentDictionary<Type, JobQueueBase> _allQueues = new();

    protected abstract Task StoreJobAsync(object command, CancellationToken ct);

    internal static Task AddToQueueAsync(ICommand command, CancellationToken ct)
    {
        var tCommand = command.GetType();

        if (!_allQueues.TryGetValue(tCommand, out var queue))
            throw new InvalidOperationException($"A job queue has not been registered for [{tCommand.FullName}]");

        return queue.StoreJobAsync(command, ct);
    }
}

public sealed class JobQueue<TCommand, TStorageRecord, TStorageProvider> : JobQueueBase
    where TCommand : ICommand
    where TStorageRecord : IJobStorageRecord, new()
    where TStorageProvider : IJobStorageProvider<TStorageRecord>
{
    private static int _concurrencyLimit;
    private static TimeSpan _executionTimeLimit;
    private static readonly Type tCommand = typeof(TCommand);
    private static readonly string queueID = tCommand.FullName!.ToHash();
    private readonly IJobStorageProvider<TStorageRecord> _storage;
    private readonly CancellationToken _ct;

    public JobQueue(IJobStorageProvider<TStorageRecord> storageProvider, IHostApplicationLifetime appLife)
    {
        _storage = storageProvider;
        _allQueues[tCommand] = this;
        _ct = appLife.ApplicationStopping;
        _ = CommandExecutorTask();
    }

    internal static void SetOptions(int concurrencyLimit, TimeSpan executionTimeLimit)
    {
        _concurrencyLimit = concurrencyLimit;
        _executionTimeLimit = executionTimeLimit;
    }

    protected override Task StoreJobAsync(object command, CancellationToken ct)
    {
        return _storage.StoreJobAsync(new TStorageRecord
        {
            QueueID = queueID,
            Command = command
        }, ct);
    }

    private async Task CommandExecutorTask()
    {
        var records = Enumerable.Empty<TStorageRecord>();

        while (!_ct.IsCancellationRequested)
        {
            try
            {
                records = await _storage.GetNextBatchAsync(
                    match: r => r.QueueID == queueID && r.LockExpiry == null,
                    batchSize: _concurrencyLimit * 2,
                    ct: _ct);
            }
            catch
            {
                //todo: log error
                await Task.Delay(5000);
                continue;
            }

            if (!records.Any())
            {
                await Task.Delay(300);
                continue;
            }

            await Parallel.ForEachAsync(
                records,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = _concurrencyLimit,
                    CancellationToken = new CancellationTokenSource(_executionTimeLimit).Token
                },
                ExecuteCommand);
        }
    }

    private async Task StaleJobPurgingTask()
    {
        while (!_ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(1));
            try
            {
                await _storage.PurgeStaleJobsAsync(
                    match: r => r.QueueID == queueID && (r.IsComplete || (r.LockExpiry != null && DateTime.UtcNow >= r.LockExpiry)),
                    ct: _ct);
            }
            catch { }
        }
    }

    private async ValueTask ExecuteCommand(TStorageRecord record, CancellationToken ct)
    {
        var cmd = (TCommand)record.Command;

        try
        {
            await cmd.ExecuteAsync(ct);
        }
        catch (Exception)
        {

            throw;
        }
    }
}

public interface IJobStorageRecord
{
    public string QueueID { get; set; }
    public object Command { get; set; }
    public DateTime? LockExpiry { get; set; }
    public bool IsComplete { get; set; }
}

public interface IJobStorageProvider<TStorageRecord> where TStorageRecord : IJobStorageRecord
{
    public Task StoreJobAsync(TStorageRecord job, CancellationToken ct);
    public Task<IEnumerable<TStorageRecord>> GetNextBatchAsync(Expression<Func<TStorageRecord, bool>> match, int batchSize, CancellationToken ct);
    public Task MarkJobAsCompleteAsync(TStorageRecord job, CancellationToken ct);
    public Task PurgeStaleJobsAsync(Expression<Func<TStorageRecord, bool>> match, CancellationToken ct);
}