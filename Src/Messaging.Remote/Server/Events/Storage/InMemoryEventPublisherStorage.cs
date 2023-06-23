using System.Collections.Concurrent;

namespace FastEndpoints;

//NOTE: this is a singleton class
internal sealed class InMemoryEventPublisherStorage : IEventPublisherStorageProvider
{
    //key: subscriber ID (identifies a unique subscriber/client)
    //val: in memory event storage record queue
    private readonly ConcurrentDictionary<string, EventQueue<IEventStorageRecord>> _subscribers = new();

    public ValueTask<IEnumerable<string>> RestoreSubsriberIDsForEventType(string eventType)
        => ValueTask.FromResult(Enumerable.Empty<string>());

    public ValueTask StoreEventAsync(IEventStorageRecord e, CancellationToken _)
    {
        var q = _subscribers.GetOrAdd(e.SubscriberID, QueueInitializer());

        if (!q.IsStale)
            q.Records.Enqueue(e);

        return ValueTask.CompletedTask;
    }

    public ValueTask<IEventStorageRecord?> GetNextEventAsync(string subscriberID, CancellationToken ct)
    {
        var q = _subscribers.GetOrAdd(subscriberID, QueueInitializer());
        q.Records.TryPeek(out var e);
        return ValueTask.FromResult(e);
    }

    public ValueTask MarkEventAsCompleteAsync(IEventStorageRecord e, CancellationToken ct)
    {
        var q = _subscribers.GetOrAdd(e.SubscriberID, QueueInitializer());
        q.Records.TryDequeue(out _);
        q.LastDequeuAt = DateTime.UtcNow;
        return ValueTask.CompletedTask;
    }

    public ValueTask PurgeStaleRecordsAsync()
    {
        foreach (var q in _subscribers)
        {
            if (q.Value.IsStale)
                _subscribers.Remove(q.Key, out _);
        }
        return ValueTask.CompletedTask;
    }

    private static EventQueue<IEventStorageRecord> QueueInitializer() => new();
}