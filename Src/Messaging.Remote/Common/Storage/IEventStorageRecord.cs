namespace FastEndpoints;

/// <summary>
/// interface for implementing an event storage record that encapsulates/embeds an event (<see cref="IEvent"/>)
/// </summary>
public interface IEventStorageRecord
{
    /// <summary>
    /// a subscriber id is a uniqu identifier of an event stream subscriber on a remote node.
    /// it is a unique id per each event handler type (TEvent+TEventHandler combo).
    /// you don't have to worry about generating this as it will automatically be set by the library.
    /// </summary>
    string SubscriberID { get; set; }

    /// <summary>
    /// the actual event object that will be embedded in the storage record.
    /// if your database doesn't support embedding objects, you may have to serialize the object and store it in this property.
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
}