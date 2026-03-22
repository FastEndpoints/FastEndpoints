namespace FastEndpoints;

record Subscriber
{
    // ReSharper disable once UnusedMember.Local
    public bool IsConnected => ConnectionCount > 0;
    public SemaphoreSlim Sem { get; } = new(0); //semaphorslim for waiting on record availability
    public int ConnectionCount { get; init; }
    public DateTime LastSeenUtc { get; init; } = DateTime.UtcNow;
    public bool IsKnownSubscriber { get; init; }
}
