namespace FastEndpoints;

/// <summary>
/// interface for implementing an event storage record that encapsulates/embeds an event (<see cref="IEvent" />)
/// </summary>
public interface IEventStorageRecord
{
    /// <summary>
    /// a subscriber id is a unique identifier of an event stream subscriber on a remote node.
    /// it is a unique id per each event handler type (TEvent+TEventHandler combo).
    /// you don't have to worry about generating this as it will automatically be set by the library.
    /// </summary>
    string SubscriberID { get; set; }

    /// <summary>
    /// the actual event object that will be embedded in the storage record.
    /// if your database/orm (such as ef-core) doesn't support embedding objects, you can take the following steps:
    /// <code>
    /// 1. add a [NotMapped] attribute to this property.
    /// 2. add a new property, either a <see langword="string" /> or <see cref="byte" /> array
    /// 3. implement both <see cref="GetEvent{TEvent}" /> and <see cref="SetEvent{TEvent}" /> to serialize/deserialize the event object back and forth and store it in the newly added property.
    /// </code>
    /// you may use any serializer you please. recommendation is to use MessagePack.
    /// </summary>
    object Event { get; set; }

    /// <summary>
    /// the full type name of the event
    /// </summary>
    string EventType { get; set; }

    /// <summary>
    /// the expiration date/time of the event. this is used to purge stale records.
    /// default value is 4 hours from time of creation.
    /// </summary>
    DateTime ExpireOn { get; set; }

    /// <summary>
    /// pending status of the event. will only return true if the event has been successfully processed and is ready to be discarded.
    /// </summary>
    bool IsComplete { get; set; }

    /// <summary>
    /// implement this function to customize event deserialization.
    /// </summary>
    TEvent GetEvent<TEvent>() where TEvent : IEvent
        => (TEvent)Event;

    /// <summary>
    /// implement this method to customize event serialization.
    /// </summary>
    void SetEvent<TEvent>(TEvent @event) where TEvent : IEvent
        => Event = @event;
}