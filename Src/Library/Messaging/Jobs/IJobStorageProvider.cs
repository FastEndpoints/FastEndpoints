using System.Linq.Expressions;

namespace FastEndpoints;

public interface IJobStorageProvider<TStorageRecord> where TStorageRecord : IJobStorageRecord
{
    Task StoreJobAsync(TStorageRecord job, CancellationToken ct);
    Task<IEnumerable<TStorageRecord>> GetNextBatchAsync(Expression<Func<TStorageRecord, bool>> match, int batchSize, CancellationToken ct);
    Task MarkJobAsCompleteAsync(TStorageRecord job, CancellationToken ct);
    Task OnHandlerExecutionFailureAsync(TStorageRecord job, Exception exception, CancellationToken ct);
    Task PurgeStaleJobsAsync(Expression<Func<TStorageRecord, bool>> match, CancellationToken ct);
}