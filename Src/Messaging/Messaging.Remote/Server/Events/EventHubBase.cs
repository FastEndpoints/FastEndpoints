using System.Collections.Concurrent;

namespace FastEndpoints;

abstract class EventHubBase
{
    //key: tEvent
    //val: event hub for the event type
    //values get created when the DI container resolves each event hub type and the ctor is run.
    protected static readonly ConcurrentDictionary<Type, EventHubBase> AllHubs = new();

    protected bool IsInMemoryProvider { get; init; }

    protected abstract Task Initialize();

    protected abstract Task BroadcastEventTask(IEvent evnt);

    internal static Task InitializeHubs()
        => Task.WhenAll(AllHubs.Values.Where(hub => !hub.IsInMemoryProvider).Select(hub => hub.Initialize()));

    internal static void AddToSubscriberQueues(IEvent evnt)
    {
        var tEvent = evnt.GetType();

        if (AllHubs.TryGetValue(tEvent, out var hub))
            _ = hub.BroadcastEventTask(evnt); //executed in background. will never throw exceptions.
        else
            throw new InvalidOperationException($"An event hub has not been registered for [{tEvent.FullName}]");
    }
}
