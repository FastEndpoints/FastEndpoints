using System.Collections.Concurrent;

namespace FastEndpoints;

//NOTE: this is a singleton class
internal sealed class InMemoryEventHubStorage : IEventHubStorageProvider
{
    //key: subscriber ID (identifies a unique subscriber/client)
    //val: in memory event storage record queue
    private readonly ConcurrentDictionary<string, EventQueue> _subscribers = new();

    public ValueTask<IEnumerable<string>> RestoreSubsriberIDsForEventType(string eventType)
        => ValueTask.FromResult(Enumerable.Empty<string>());

    public ValueTask StoreEventAsync(IEventStorageRecord e, CancellationToken _)
    {
        var q = _subscribers.GetOrAdd(e.SubscriberID, new EventQueue());

        if (!q.IsStale)
            q.Records.Enqueue(e);
        else
            throw new OverflowException();

        return ValueTask.CompletedTask;
    }

    public ValueTask<IEventStorageRecord?> GetNextEventAsync(string subscriberID, CancellationToken ct)
    {
        var q = _subscribers.GetOrAdd(subscriberID, new EventQueue());

        q.Records.TryDequeue(out var e);
        q.LastDequeuAt = DateTime.UtcNow;

        return ValueTask.FromResult(e);
    }

    public ValueTask MarkEventAsCompleteAsync(IEventStorageRecord e, CancellationToken ct)
        => throw new NotImplementedException();

    public ValueTask PurgeStaleRecordsAsync()
    {
        foreach (var q in _subscribers)
        {
            if (q.Value.IsStale)
                _subscribers.Remove(q.Key, out _);
        }
        return ValueTask.CompletedTask;
    }
}