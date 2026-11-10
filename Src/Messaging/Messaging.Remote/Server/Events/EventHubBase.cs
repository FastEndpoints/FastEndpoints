using System.Collections.Concurrent;

namespace FastEndpoints;

abstract class EventHubBase
{
    //key: tEvent
    //val: event hub for the event type
    //values get created when the DI container resolves each event hub type and the ctor is run.
    protected static readonly ConcurrentDictionary<Type, EventHubBase> AllHubs = new();

    protected bool IsInMemoryProvider { get; init; }
    protected EventHubRuntime Runtime { get; init; } = null!;

    protected abstract Task Initialize();

    protected abstract Task BroadcastEventTask(IEvent evnt);

    internal Task InitializeIfNeeded()
        => IsInMemoryProvider ? Task.CompletedTask : Initialize();

    internal void Unregister()
    {
        foreach (var entry in AllHubs.Where(entry => ReferenceEquals(entry.Value, this)))
            AllHubs.TryRemove(entry);
    }

    protected void QueueEvent(IEvent evnt)
        => Runtime.Broadcast((Hub: this, Event: evnt), static state => state.Hub.BroadcastEventTask(state.Event));

    internal static void AddToSubscriberQueues(IEvent evnt)
    {
        var tEvent = evnt.GetType();

        if (AllHubs.TryGetValue(tEvent, out var hub))
            hub.QueueEvent(evnt);
        else
            throw new InvalidOperationException($"An event hub has not been registered for [{tEvent.FullName}]");
    }
}
