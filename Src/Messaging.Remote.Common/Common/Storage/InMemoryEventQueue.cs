namespace FastEndpoints;

/// <summary>
/// provides some global configuration options for the in-memory event queues
/// </summary>
public static class InMemoryEventQueue
{
    /// <summary>
    /// the maximum number of items the internal queues are allowed to hold.
    /// when the item count surpasses this limit, the queues will be in an overflow state preventing acceptance of new events.
    /// <para>
    /// NOTE: this limit applies per event type. i.e. if there's 10 event types, the total events held in memory will be 10X of this value.
    /// </para>
    /// </summary>
    public static int MaxLimit { get; set; } = 1000;
}