namespace FastEndpoints;

/// <summary>
/// encapsulates the behavioral differences between in-memory and durable subscriber storage providers.
/// this eliminates scattered boolean checks throughout the receiver/executor logic.
/// </summary>
sealed class SubscriberStorageBehavior
{
    internal static readonly SubscriberStorageBehavior InMemory = new(isInMemory: true);
    internal static readonly SubscriberStorageBehavior Durable = new(isInMemory: false);

    readonly bool _isInMemory;

    SubscriberStorageBehavior(bool isInMemory)
    {
        _isInMemory = isInMemory;
    }

    internal static SubscriberStorageBehavior For<TStorageProvider>(TStorageProvider provider)
        => provider is InMemoryEventSubscriberStorage ? InMemory : Durable;

    /// <summary>
    /// durable providers must persist the received event even during app shutdown to prevent data loss,
    /// so they use <see cref="CancellationToken.None" />. in-memory providers use the app token since
    /// there's nothing to persist beyond the process lifetime.
    /// </summary>
    internal CancellationToken GetStoreEventToken(CancellationToken appToken)
        => _isInMemory ? appToken : CancellationToken.None;

    /// <summary>
    /// for in-memory providers, fetching dequeues records from the queue, so only fetch exactly
    /// the number of available slots to prevent losing events that can't be immediately assigned
    /// to an execution slot. durable providers do not lease records, so a refill may need to
    /// look past "still running" records and requires a full concurrency-sized window.
    /// </summary>
    internal int GetFetchLimit(int maxConcurrency, int activeCount)
        => _isInMemory ? maxConcurrency - activeCount : maxConcurrency;

    /// <summary>
    /// durable providers retry handler execution in-place. in-memory providers re-queue the event
    /// to the back of the queue and release the execution slot immediately.
    /// </summary>
    internal bool ShouldRetryHandlerOnFailure => !_isInMemory;

    /// <summary>
    /// durable providers need an explicit completion marker so events are not replayed.
    /// in-memory providers dequeue on read, so no completion tracking is needed.
    /// </summary>
    internal bool ShouldMarkComplete => !_isInMemory;
}