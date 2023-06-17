namespace FastEndpoints;

public static class EventExtensions
{
    public static void Broadcast<TEvent>(this TEvent evnt) where TEvent : class, IEvent
        => EventHub<TEvent>.BroadcastEvent(evnt);
}
