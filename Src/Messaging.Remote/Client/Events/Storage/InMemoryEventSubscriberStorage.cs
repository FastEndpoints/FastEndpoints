using System.Collections.Concurrent;

namespace FastEndpoints;

//NOTE: this is a singleton class
internal sealed class InMemoryEventSubscriberStorage : IEventSubscriberStorageProvider
{
    private const int max_queue_size = 1000;

    //key: subscriber ID (see EventHandlerExecutor.ctor to see how subscriber id is generated)
    //val: queue of events for the subscriber
    private readonly ConcurrentDictionary<string, ConcurrentQueue<IEventStorageRecord>> _subscribers = new();

    public ValueTask StoreEventAsync(IEventStorageRecord e, CancellationToken _)
    {
        var q = _subscribers.GetOrAdd(e.SubscriberID, QueueInitializer());

        if (q.Count >= max_queue_size)
            throw new OverflowException("In-memory event receive queue limit reached!");

        q.Enqueue(e);

        return ValueTask.CompletedTask;
    }

    public ValueTask<IEventStorageRecord?> GetNextEventAsync(string subscriberID, CancellationToken _)
    {
        var q = _subscribers.GetOrAdd(subscriberID, QueueInitializer());
        q.TryPeek(out var e);
        return ValueTask.FromResult(e);
    }

    public ValueTask MarkEventAsCompleteAsync(IEventStorageRecord e, CancellationToken ct)
    {
        var q = _subscribers.GetOrAdd(e.SubscriberID, QueueInitializer());
        q.TryDequeue(out _);
        return ValueTask.CompletedTask;
    }

    public ValueTask PurgeStaleRecordsAsync() => throw new NotImplementedException();

    private static ConcurrentQueue<IEventStorageRecord> QueueInitializer() => new();
}