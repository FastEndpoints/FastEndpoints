using System.Collections.Concurrent;

namespace FastEndpoints;

internal sealed class EventQueue<TStorageRecord> where TStorageRecord : IEventStorageRecord
{
    private const int max_queue_size = 100000;

    internal ConcurrentQueue<TStorageRecord> Records { get; set; } = new();
    internal DateTime LastDequeuAt { get; set; } = DateTime.UtcNow;
    internal bool IsStale => (Records.Count >= max_queue_size && DateTime.UtcNow.Subtract(LastDequeuAt).TotalHours >= 1) ||
                             (!Records.IsEmpty && DateTime.UtcNow.Subtract(LastDequeuAt).TotalHours >= 4);
}