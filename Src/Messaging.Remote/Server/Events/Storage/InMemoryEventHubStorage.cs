using System.Collections.Concurrent;

namespace FastEndpoints;

//NOTE: this is a singleton class
public sealed class InMemoryEventHubStorage : IEventHubStorageProvider<InMemoryEventStorageRecord>
{
    //key: subscriber ID (identifies a unique subscriber/client)
    //val: in memory event storage record queue
    readonly ConcurrentDictionary<string, InMemEventQueue> _subscribers = new();

    public ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<InMemoryEventStorageRecord> p)
        => ValueTask.FromResult(Enumerable.Empty<string>());

    public ValueTask StoreEventAsync(InMemoryEventStorageRecord e, CancellationToken _)
    {
        var q = _subscribers.GetOrAdd(e.SubscriberID, new InMemEventQueue());

        if (!q.IsStale)
            q.Records.Enqueue(e);
        else
            throw new OverflowException();

        return ValueTask.CompletedTask;
    }

    public ValueTask<IEnumerable<InMemoryEventStorageRecord>> GetNextBatchAsync(PendingRecordSearchParams<InMemoryEventStorageRecord> p)
    {
        var q = _subscribers.GetOrAdd(p.SubscriberID, new InMemEventQueue());

        q.Records.TryDequeue(out var e);
        q.LastDequeAt = DateTime.UtcNow;

        if (e is not null)
        {
            var res = new[] { e };

            return ValueTask.FromResult(res.AsEnumerable());
        }

        return ValueTask.FromResult(Array.Empty<InMemoryEventStorageRecord>().AsEnumerable());
    }

    public ValueTask MarkEventAsCompleteAsync(InMemoryEventStorageRecord e, CancellationToken ct)
        => throw new NotImplementedException();

    public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<InMemoryEventStorageRecord> p)
    {
        foreach (var q in _subscribers)
        {
            if (q.Value.IsStale)
            {
                _subscribers.Remove(q.Key, out var eq);
                eq?.Records.Clear();
            }
        }

        return ValueTask.CompletedTask;
    }
}