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
    private static readonly ParallelOptions _parallelOpts = new() { MaxDegreeOfParallelism = 1 };
    private static readonly Type tCommand = typeof(TCommand);
    private static readonly string queueID = tCommand.FullName!.ToHash();
    private readonly IJobStorageProvider<TStorageRecord> _storage;
    private readonly CancellationToken _ct;

    public JobQueue(IJobStorageProvider<TStorageRecord> storageProvider, IHostApplicationLifetime appLife)
    {
        _storage = storageProvider;
        _allQueues[tCommand] = this;
        _ct = appLife.ApplicationStopping;
        _parallelOpts.CancellationToken = _ct;
        _ = CommandExecutorTask();
    }

    internal static void SetMaxParallelism(int maxLimit)
        => _parallelOpts.MaxDegreeOfParallelism = maxLimit;

    protected override Task StoreJobAsync(object command, CancellationToken ct)
    {
        return _storage.StoreJobAsync(new TStorageRecord
        {
            Command = command,
            IsLocked = false,
            QueueID = queueID
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
                    match: r => r.QueueID == queueID && !r.IsLocked,
                    batchSize: _parallelOpts.MaxDegreeOfParallelism * 2,
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

            await Parallel.ForEachAsync(records, _parallelOpts, ExecuteCommand);
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
    public bool IsLocked { get; set; }
}

public interface IJobStorageProvider<TStorageRecord> where TStorageRecord : IJobStorageRecord
{
    public Task StoreJobAsync(TStorageRecord job, CancellationToken ct);
    public Task<IEnumerable<TStorageRecord>> GetNextBatchAsync(Expression<Func<TStorageRecord, bool>> match, int batchSize, CancellationToken ct);
    public Task DeleteJobAsync(TStorageRecord job, CancellationToken ct);
}