namespace FastEndpoints;

/// <summary>
/// enum for specifying which mode the event hub should be running in.
/// </summary>
[Flags]
public enum HubMode
{
    /// <summary>
    /// this server/application itself is the sole publisher of events. no external publishers will be accepted. this is the default mode.
    /// </summary>
    EventPublisher = 0,

    /// <summary>
    /// enable remote event publishers to send events to this server which will be relayed to the connected subscribers.
    /// this mode also allows this server itself to publish events as well.
    /// </summary>
    EventBroker = 1,

    /// <summary>
    /// with this mode events will only be delivered to just one of the subscribers connected to the hub in a round robin fashion.
    /// if for example, there's two subscribers (A and B) connected, event 1 will be delivered to subscriber A.
    /// event 2 will be delivered to subscriber B. event 3 will be delivered to subscriber A again and so on.
    /// </summary>
    RoundRobin = 2
}
