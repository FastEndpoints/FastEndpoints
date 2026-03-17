namespace FastEndpoints;

public sealed class InMemoryEventStorageRecord : IEventStorageRecord
{
    public string SubscriberID { get; set; } = default!;
    public Guid TrackingID { get; set; }
    public object Event { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public DateTime ExpireOn { get; set; } = DateTime.UtcNow.AddHours(4);
    public bool IsComplete { get; set; }
    public bool QueueOverflowed { get; set; }
}
