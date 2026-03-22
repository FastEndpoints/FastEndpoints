namespace FastEndpoints;

/// <summary>
/// centralizes all timing constants used by the event subscriber receiver and executor tasks.
/// having them in one place makes them easy to find, tune, and override in tests.
/// </summary>
static class SubscriberTimings
{
    /// <summary>
    /// delay between reconnection attempts when the gRPC stream is broken or a transient error occurs.
    /// </summary>
    internal static readonly TimeSpan ReceiverReconnectDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// delay after a failed attempt to retrieve the next batch of events from storage.
    /// </summary>
    internal static readonly TimeSpan StorageRetrievalErrorDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// delay between retries when marking an event as complete fails.
    /// </summary>
    internal static readonly TimeSpan MarkCompleteRetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// delay between retries when handler execution fails (durable providers only).
    /// this field is non-readonly so tests can override it via reflection.
    /// </summary>
    internal static TimeSpan HandlerExecutionRetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// how long the executor waits for a semaphore signal before polling storage again.
    /// acts as the maximum idle interval between fetch cycles.
    /// </summary>
    internal static readonly TimeSpan ExecutorPollInterval = TimeSpan.FromSeconds(10);
}