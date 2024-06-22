using System.Collections.Concurrent;

namespace FastEndpoints;

sealed class InMemEventQueue
{
    internal ConcurrentQueue<InMemoryEventStorageRecord> Records { get; set; } = new();
    internal DateTime LastDequeAt { get; set; } = DateTime.UtcNow;
    internal bool IsStale => (Records.Count >= InMemoryEventQueue.MaxLimit && DateTime.UtcNow.Subtract(LastDequeAt).TotalHours >= 1) ||
                             (!Records.IsEmpty && DateTime.UtcNow.Subtract(LastDequeAt).TotalHours >= 4);
}