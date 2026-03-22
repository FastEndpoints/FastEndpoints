namespace FastEndpoints;

/// <summary>
/// centralizes all timing constants and sizing defaults used by the event hub dispatcher and broadcast tasks.
/// having them in one place makes them easy to find, tune, and override in tests.
/// </summary>
static class EventHubSettings
{
    /// <summary>
    /// delay between retries when a storage operation fails (get-next-batch, store-events, mark-complete, restore-subscriber-ids).
    /// </summary>
    internal static readonly TimeSpan StorageRetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// maximum time allowed for restoring subscriber IDs from the storage provider during initialization.
    /// if the timeout is exceeded the application will not be allowed to start.
    /// </summary>
    internal static readonly TimeSpan InitializationTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// maximum number of event records fetched from storage per subscriber per iteration.
    /// </summary>
    internal static readonly int BatchSize = 25;

    /// <summary>
    /// default time-to-live for persisted event records.
    /// </summary>
    internal static readonly TimeSpan EventExpiry = TimeSpan.FromHours(4);

    /// <summary>
    /// delay between polls when no subscribers are registered for an event being broadcast.
    /// </summary>
    internal static readonly TimeSpan NoSubscriberRetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// maximum time to wait for at least one subscriber to appear before silently dropping a broadcast event.
    /// </summary>
    internal static readonly TimeSpan NoSubscriberTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// how long the dispatcher waits for a semaphore signal before polling storage again.
    /// acts as the maximum idle interval between fetch cycles.
    /// this field is non-readonly so tests can override it.
    /// </summary>
    internal static TimeSpan WaitForSignalTimeout = TimeSpan.FromSeconds(10);
}