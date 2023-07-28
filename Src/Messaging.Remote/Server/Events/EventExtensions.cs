namespace FastEndpoints;

public static class EventExtensions
{
    /// <summary>
    /// broadcast/publish an event to all remote subscribers.
    /// this method should only be called when the server is running in <see cref="HubMode.EventPublisher"/>
    /// </summary>
    /// <typeparam name="TEvent">the type of the event being broadcasted</typeparam>
    public static void Broadcast<TEvent>(this TEvent @event, CancellationToken ct = default) where TEvent : class, IEvent
        => _ = EventHubBase.AddToSubscriberQueues(@event, ct);
}