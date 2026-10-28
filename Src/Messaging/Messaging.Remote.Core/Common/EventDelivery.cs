namespace FastEndpoints;

/// <summary>
/// one event pushed by an opted-in hub on the <c>sub-ack</c> stream.
/// </summary>
/// <typeparam name="TEvent">the event type</typeparam>
public sealed class EventDelivery<TEvent> where TEvent : class, IEvent
{
    /// <summary>
    /// the hub-assigned delivery id. the subscriber stores the event under this id and acknowledges it.
    /// </summary>
    public Guid TrackingID { get; set; }

    /// <summary>
    /// the event payload.
    /// </summary>
    public TEvent Event { get; set; } = null!;

    /// <summary>
    /// UTC expiry of the hub row, after which the hub no longer starts replaying this delivery.
    /// </summary>
    public DateTime ReplayUntil { get; set; }
}

/// <summary>
/// client message on the <c>sub-ack</c> stream.
/// the first message is the hello (<see cref="SubscriberID" /> set, <see cref="TrackingID" /> empty).
/// each later message acknowledges one stored delivery.
/// </summary>
public sealed class EventDeliveryAck
{
    /// <summary>
    /// subscriber id. set on the first message only.
    /// </summary>
    public string? SubscriberID { get; set; }

    /// <summary>
    /// tracking id of the delivery that was stored. empty on the hello.
    /// </summary>
    public Guid TrackingID { get; set; }
}
