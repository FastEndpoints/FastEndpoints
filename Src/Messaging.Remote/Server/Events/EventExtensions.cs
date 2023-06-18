namespace FastEndpoints;

public static class EventExtensions
{
    /// <summary>
    /// broadcast/publish an event to all remote subscribers
    /// </summary>
    /// <typeparam name="TEvent">the type of the event being broadcasted</typeparam>
    public static void Broadcast<TEvent>(this TEvent evnt) where TEvent : class, IEvent
        => EventHub<TEvent>.AddToSubscriberQueues(evnt);
}
