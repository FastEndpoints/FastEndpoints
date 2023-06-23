using System.Collections.Concurrent;

namespace FastEndpoints;

internal sealed class EventQueue<TStorageRecord> where TStorageRecord : IEventStorageRecord
{
    internal ConcurrentQueue<TStorageRecord> Records { get; set; } = new();
    internal DateTime LastDequeuAt { get; set; } = DateTime.UtcNow;
    internal bool IsStale => (Records.Count >= 1000 && DateTime.UtcNow.Subtract(LastDequeuAt).TotalHours >= 1) ||
                             (!Records.IsEmpty && DateTime.UtcNow.Subtract(LastDequeuAt).TotalHours >= 4);
}