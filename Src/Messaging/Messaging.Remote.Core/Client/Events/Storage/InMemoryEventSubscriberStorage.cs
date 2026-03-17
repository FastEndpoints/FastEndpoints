using System.Collections.Concurrent;

namespace FastEndpoints;

//NOTE: this is a singleton class
sealed class InMemoryEventSubscriberStorage : IEventSubscriberStorageProvider<InMemoryEventStorageRecord>
{
    //key: subscriber ID + event type (allows explicit subscriber ids to be reused across different event types)
    //val: queue of events for the subscriber
    readonly ConcurrentDictionary<string, ConcurrentQueue<InMemoryEventStorageRecord>> _subscribers = new();

    public ValueTask StoreEventAsync(InMemoryEventStorageRecord e, CancellationToken _)
    {
        var q = _subscribers.GetOrAdd(GetQueueKey(e.SubscriberID, e.EventType), QueueInitializer());

        if (q.Count >= InMemoryEventQueue.MaxLimit)
            throw new OverflowException("In-memory event receive queue limit reached!");

        q.Enqueue(e);

        return default;
    }

    public ValueTask<IEnumerable<InMemoryEventStorageRecord>> GetNextBatchAsync(PendingRecordSearchParams<InMemoryEventStorageRecord> p)
    {
        var q = _subscribers.GetOrAdd(GetQueueKey(p.SubscriberID, p.EventType), QueueInitializer());
        q.TryDequeue(out var e);

        return new(e is null ? [] : [e]);
    }

    public ValueTask MarkEventAsCompleteAsync(InMemoryEventStorageRecord e, CancellationToken ct)
        => throw new NotImplementedException();

    public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<InMemoryEventStorageRecord> parameters)
        => throw new NotImplementedException();

    static ConcurrentQueue<InMemoryEventStorageRecord> QueueInitializer()
        => new();

    static string GetQueueKey(string subscriberId, string eventType)
        => subscriberId + "|" + eventType;
}
