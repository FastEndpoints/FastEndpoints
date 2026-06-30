// ReSharper disable UnusedParameter.Global

namespace FastEndpoints;

/// <summary>
/// inherit this class and override it's methods in order to receive event hub exceptions.
/// </summary>
public abstract class EventHubExceptionReceiver
{
    /// <summary>
    /// this method is triggered when the storage provider has trouble restoring event subscribers.
    /// </summary>
    /// <param name="eventType">the type of the event</param>
    /// <param name="attemptCount">the number of times the subscriber were attempted to be retrieved</param>
    /// <param name="exception">the actual exception that was thrown by the operation</param>
    /// <param name="ct">cancellation token</param>
    public virtual Task OnRestoreSubscriberIDsError(Type eventType, int attemptCount, Exception exception, CancellationToken ct)
        => Task.CompletedTask;

    /// <summary>
    /// this method is triggered when the storage provider has trouble retrieving the next batch of event records.
    /// </summary>
    /// <typeparam name="TEvent">the type of the event</typeparam>
    /// <param name="subscriberID">the unique ID of the subscriber</param>
    /// <param name="attemptCount">the number of times the operation was attempted </param>
    /// <param name="exception">the actual exception that was thrown by the operation</param>
    /// <param name="ct">cancellation token</param>
    public virtual Task OnGetNextBatchError<TEvent>(string subscriberID, int attemptCount, Exception exception, CancellationToken ct)
        where TEvent : class, IEvent
        => Task.CompletedTask;

    /// <summary>
    /// this method is triggered when the storage provider has trouble marking an event record as complete.
    /// </summary>
    /// <typeparam name="TEvent">the type of the event</typeparam>
    /// <param name="record">the event storage record that was supposed to be marked complete</param>
    /// <param name="attemptCount">the number of times the record was attempted to be marked complete</param>
    /// <param name="exception">the actual exception that was thrown by the operation</param>
    /// <param name="ct">cancellation token</param>
    public virtual Task OnMarkEventAsCompleteError<TEvent>(IEventStorageRecord record, int attemptCount, Exception exception, CancellationToken ct)
        where TEvent : class, IEvent
        => Task.CompletedTask;

    /// <summary>
    /// this method is triggered when the storage provider has trouble persisting event storage records.
    /// </summary>
    /// <typeparam name="TEvent">the type of the events</typeparam>
    /// <param name="records">the event storage records that were supposed to be persisted</param>
    /// <param name="attemptCount">the number of times the operation was attempted</param>
    /// <param name="exception">the actual exception that was thrown by the operation</param>
    /// <param name="ct">cancellation token</param>
    public virtual Task OnStoreEventRecordsError<TEvent>(IEnumerable<IEventStorageRecord> records, int attemptCount, Exception exception, CancellationToken ct)
        where TEvent : class, IEvent
        => Task.CompletedTask;

    /// <summary>
    /// this method is triggered when the default in-memory storage provider's internal queue for the given event type has been stagnant and in an overflow state.
    /// </summary>
    /// <typeparam name="TEvent">the type of the event</typeparam>
    /// <param name="record">the event storage record that was supposed to be added to the queue</param>
    /// <param name="ct">cancellation token</param>
    public virtual Task OnInMemoryQueueOverflow<TEvent>(IEventStorageRecord record, CancellationToken ct) where TEvent : class, IEvent
        => Task.CompletedTask;

    /// <summary>
    /// this method is triggered when the storage provider has trouble serializing an event object calling the
    /// <see cref="IEventStorageRecord" />.<see cref="IEventStorageRecord.SetEvent{TEvent}" /> method.
    /// </summary>
    /// <typeparam name="TEvent">the type of the event</typeparam>
    /// <param name="event">the event object that failed to serialize</param>
    /// <param name="exception">the actual exception that was thrown by the operation</param>
    /// <param name="ct">cancellation token</param>
    public virtual Task OnSerializeEventError<TEvent>(TEvent @event, Exception exception, CancellationToken ct)
        where TEvent : class, IEvent
        => Task.CompletedTask;
}