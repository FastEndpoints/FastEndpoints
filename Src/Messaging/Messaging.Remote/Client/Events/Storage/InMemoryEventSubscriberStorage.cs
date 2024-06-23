using System.Collections.Concurrent;

namespace FastEndpoints;

//NOTE: this is a singleton class
sealed class InMemoryEventSubscriberStorage : IEventSubscriberStorageProvider<InMemoryEventStorageRecord>
{
    //key: subscriber ID (see EventSubscriber.ctor to see how subscriber id is generated)
    //val: queue of events for the subscriber
    readonly ConcurrentDictionary<string, ConcurrentQueue<InMemoryEventStorageRecord>> _subscribers = new();

    public ValueTask StoreEventAsync(InMemoryEventStorageRecord e, CancellationToken _)
    {
        var q = _subscribers.GetOrAdd(e.SubscriberID, QueueInitializer());

        if (q.Count >= InMemoryEventQueue.MaxLimit)
            throw new OverflowException("In-memory event receive queue limit reached!");

        q.Enqueue(e);

        return ValueTask.CompletedTask;
    }

    public ValueTask<IEnumerable<InMemoryEventStorageRecord>> GetNextBatchAsync(PendingRecordSearchParams<InMemoryEventStorageRecord> p)
    {
        var q = _subscribers.GetOrAdd(p.SubscriberID, QueueInitializer());
        q.TryDequeue(out var e);

        if (e is not null)
        {
            var res = new[] { e };

            return ValueTask.FromResult(res.AsEnumerable());
        }

        return ValueTask.FromResult(Array.Empty<InMemoryEventStorageRecord>().AsEnumerable());
    }

    public ValueTask MarkEventAsCompleteAsync(InMemoryEventStorageRecord e, CancellationToken ct)
        => throw new NotImplementedException();

    public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<InMemoryEventStorageRecord> parameters)
        => throw new NotImplementedException();

    static ConcurrentQueue<InMemoryEventStorageRecord> QueueInitializer()
        => new();
}