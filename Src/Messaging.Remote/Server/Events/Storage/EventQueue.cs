using System.Collections.Concurrent;

namespace FastEndpoints;

internal sealed class EventQueue
{
    internal ConcurrentQueue<InMemoryEventStorageRecord> Records { get; set; } = new();
    internal DateTime LastDequeuAt { get; set; } = DateTime.UtcNow;
    internal bool IsStale => (Records.Count >= InMemoryEventQueue.MaxLimit && DateTime.UtcNow.Subtract(LastDequeuAt).TotalHours >= 1) ||
                             (!Records.IsEmpty && DateTime.UtcNow.Subtract(LastDequeuAt).TotalHours >= 4);
}