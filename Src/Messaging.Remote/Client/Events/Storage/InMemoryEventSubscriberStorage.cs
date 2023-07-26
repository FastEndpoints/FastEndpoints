using System.Collections.Concurrent;

namespace FastEndpoints;

//NOTE: this is a singleton class
internal sealed class InMemoryEventSubscriberStorage : IEventSubscriberStorageProvider
{
    //key: subscriber ID (see EventSubscriber.ctor to see how subscriber id is generated)
    //val: queue of events for the subscriber
    private readonly ConcurrentDictionary<string, ConcurrentQueue<IEventStorageRecord>> _subscribers = new();

    public ValueTask StoreEventAsync(IEventStorageRecord e, CancellationToken _)
    {
        var b = _subscribers.GetOrAdd(e.SubscriberID, QueueInitializer());

        if (b.Count >= InMemoryEventQueue.MaxLimit)
            throw new OverflowException("In-memory event receive queue limit reached!");

        b.Enqueue(e);

        return ValueTask.CompletedTask;
    }

    public ValueTask<IEventStorageRecord?> GetNextEventAsync(string subscriberID, CancellationToken _)
    {
        var b = _subscribers.GetOrAdd(subscriberID, QueueInitializer());
        b.TryDequeue(out var e);
        return ValueTask.FromResult(e);
    }

    public ValueTask MarkEventAsCompleteAsync(IEventStorageRecord e, CancellationToken ct)
        => throw new NotImplementedException();

    public ValueTask PurgeStaleRecordsAsync() => throw new NotImplementedException();

    private static ConcurrentQueue<IEventStorageRecord> QueueInitializer() => new();
}