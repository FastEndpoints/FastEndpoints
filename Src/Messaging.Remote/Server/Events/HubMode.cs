namespace FastEndpoints;

/// <summary>
/// enum for specifying which mode the event hub should be running in.
/// </summary>
public enum HubMode
{
    /// <summary>
    /// this server/application itself is the sole publisher of events. no external publishers will be accepted. this is the default mode.
    /// </summary>
    EventPublisher = 1,

    /// <summary>
    /// enable remote event publishers to send events to this server which will be relayed to the connected subscribers.
    /// this mode also allows this server itself to publish events as well.
    /// </summary>
    EventBroker = 2,
}
