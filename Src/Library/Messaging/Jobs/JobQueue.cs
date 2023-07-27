using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace FastEndpoints;

/// <summary>
/// base class for all <see cref="JobQueue{TCommand, TStorageRecord, TStorageProvider}"/> classes.
/// </summary>
internal abstract class JobQueueBase
{
    //key: tCommand
    //val: job queue for the command type
    //values get created when the DI container resolves each job queue type and the ctor is run.
    //see ctor in JobQueue<TCommand, TStorageRecord, TStorageProvider>
    protected static readonly ConcurrentDictionary<Type, JobQueueBase> _allQueues = new();

    protected abstract Task StoreJobAsync(object command, CancellationToken ct);

    internal abstract void SetExecutionLimits(int concurrencyLimit, TimeSpan executionTimeLimit);

    internal static Task AddToQueueAsync(ICommand command, CancellationToken ct)
    {
        var tCommand = command.GetType();

        if (!_allQueues.TryGetValue(tCommand, out var queue))
            throw new InvalidOperationException($"A job queue has not been registered for [{tCommand.FullName}]");

        return queue.StoreJobAsync(command, ct);
    }
}

/// <summary>
/// represents a job queue for a particular type of <see cref="ICommand"/>
/// </summary>
/// <typeparam name="TCommand">the type of the command</typeparam>
/// <typeparam name="TStorageRecord">the type of the job storage record</typeparam>
/// <typeparam name="TStorageProvider">the type of the job storage provider</typeparam>
internal sealed class JobQueue<TCommand, TStorageRecord, TStorageProvider> : JobQueueBase
    where TCommand : ICommand
    where TStorageRecord : IJobStorageRecord, new()
    where TStorageProvider : IJobStorageProvider<TStorageRecord>
{
    private static readonly Type _tCommand = typeof(TCommand);
    private static readonly string _queueID = _tCommand.FullName!.ToHash();
    private static readonly ParallelOptions _parallelOptions = new() { MaxDegreeOfParallelism = Environment.ProcessorCount };
    private static CancellationToken _appCancellation;
    private static IJobStorageProvider<TStorageRecord> _storage;
    private static TimeSpan _executionTimeLimit = Timeout.InfiniteTimeSpan;

    /// <summary>
    /// instantiates a job queue
    /// </summary>
    /// <param name="storageProvider">the storage provider instance to use</param>
    /// <param name="appLife">application lifetime instance to use</param>
    public JobQueue(TStorageProvider storageProvider, IHostApplicationLifetime appLife)
    {
        _allQueues[_tCommand] = this;
        _storage = storageProvider;
        _appCancellation = appLife.ApplicationStopping;
        _parallelOptions.CancellationToken = _appCancellation;
    }

    internal override void SetExecutionLimits(int concurrencyLimit, TimeSpan executionTimeLimit)
    {
        _parallelOptions.MaxDegreeOfParallelism = concurrencyLimit;
        _executionTimeLimit = executionTimeLimit;
        _ = CommandExecutorTask();
        _ = StaleJobPurgingTask();
    }

    protected override Task StoreJobAsync(object command, CancellationToken ct)
    {
        return _storage.StoreJobAsync(new TStorageRecord
        {
            QueueID = _queueID,
            Command = command,
            ExecuteAfter = DateTime.UtcNow,
            ExpireOn = DateTime.UtcNow.AddHours(4)
        }, ct); //todo: sem.Release() here
    }

    private static async Task CommandExecutorTask()
    {
        var records = Enumerable.Empty<TStorageRecord>();
        var batchSize = _parallelOptions.MaxDegreeOfParallelism * 2;

        while (!_appCancellation.IsCancellationRequested)
        {
            try
            {
                records = await _storage.GetNextBatchAsync(

                    match: r =>
                           r.QueueID == _queueID &&
                           !r.IsComplete &&
                           DateTime.UtcNow >= r.ExecuteAfter &&
                           DateTime.UtcNow <= r.ExpireOn,

                    batchSize: batchSize,

                    ct: _appCancellation);
            }
            catch
            {
                //todo: log error
                await Task.Delay(5000);
                continue;
            }

            if (!records.Any())
            {
                await Task.Delay(300); //todo: sem.WaitAsync() here
                continue;
            }

            await Parallel.ForEachAsync(records, _parallelOptions, ExecuteCommand);
        }

        static async ValueTask ExecuteCommand(TStorageRecord record, CancellationToken _)
        {
            try
            {
                await ((TCommand)record.Command)
                    .ExecuteAsync(new CancellationTokenSource(_executionTimeLimit).Token);
                //todo: log info
            }
            catch (Exception x)
            {
                //todo: log critical

                while (!_appCancellation.IsCancellationRequested)
                {
                    try
                    {
                        await _storage.OnHandlerExecutionFailureAsync(record, x, _appCancellation);
                        break;
                    }
                    catch (Exception)
                    {
                        //todo: log error

#pragma warning disable CA2016
                        await Task.Delay(5000);
#pragma warning restore CA2016
                    }
                }

                return; //abort execution here
            }

            while (!_appCancellation.IsCancellationRequested)
            {
                try
                {
                    record.IsComplete = true;
                    await _storage.MarkJobAsCompleteAsync(record, _appCancellation);
                    break;
                }
                catch (Exception)
                {
                    //todo: log error
#pragma warning disable CA2016
                    await Task.Delay(5000);
#pragma warning restore CA2016
                }
            }
        }
    }

    private static async Task StaleJobPurgingTask()
    {
        while (!_appCancellation.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(1));
            try
            {
                await _storage.PurgeStaleJobsAsync(
                    match: r => r.QueueID == _queueID && (r.IsComplete || r.ExpireOn <= DateTime.UtcNow),
                    ct: _appCancellation);
            }
            catch { }
        }
    }
}