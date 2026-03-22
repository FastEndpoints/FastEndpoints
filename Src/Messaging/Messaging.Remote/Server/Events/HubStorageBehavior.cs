namespace FastEndpoints;

/// <summary>
/// encapsulates the behavioral differences between in-memory and durable event hub storage providers.
/// this eliminates scattered boolean checks throughout the dispatcher and broadcast logic.
/// </summary>
sealed class HubStorageBehavior
{
    internal static readonly HubStorageBehavior InMemory = new(isInMemory: true);
    internal static readonly HubStorageBehavior Durable = new(isInMemory: false);

    readonly bool _isInMemory;

    HubStorageBehavior(bool isInMemory)
    {
        _isInMemory = isInMemory;
    }

    internal static HubStorageBehavior For<TStorageProvider>(TStorageProvider provider)
        => provider is InMemoryEventHubStorage ? InMemory : Durable;

    /// <summary>
    /// durable providers must persist outgoing fan-out records even during app shutdown so published
    /// events remain available for delivery after restart, so they use <see cref="CancellationToken.None" />.
    /// in-memory providers use the app token since there's nothing to persist beyond the process lifetime.
    /// </summary>
    internal CancellationToken GetStoreEventsToken(CancellationToken appToken)
        => _isInMemory ? appToken : CancellationToken.None;

    /// <summary>
    /// in-memory providers dequeue records from the queue on read, so stream write failures must re-queue
    /// the current record and all remaining unattempted records in the batch. durable providers do not
    /// dequeue on read, so the records remain available for the next fetch cycle.
    /// </summary>
    internal bool ShouldRequeueOnStreamFailure => _isInMemory;

    /// <summary>
    /// durable providers need an explicit completion marker so events are not replayed.
    /// in-memory providers dequeue on read, so no completion tracking is needed.
    /// </summary>
    internal bool ShouldMarkComplete => !_isInMemory;

    /// <summary>
    /// only durable providers need initialization (restoring subscriber IDs from storage on startup).
    /// in-memory providers start with an empty state each time.
    /// </summary>
    internal bool ShouldInitialize => !_isInMemory;
}
