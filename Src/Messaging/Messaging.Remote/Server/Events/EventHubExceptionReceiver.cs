// ReSharper disable UnusedParameter.Global

namespace FastEndpoints;

/// <summary>
/// inherit this class and override it's methods in order to receive event hub exceptions.
/// </summary>
public abstract class EventHubExceptionReceiver
{
    /// <summary>
    /// this method is triggered when the storage provider has trouble retrieving the next event record.
    /// </summary>
    /// <typeparam name="TEvent">the type of the event</typeparam>
    /// <param name="subscriberID">the unique ID of the subscriber</param>
    /// <param name="attemptCount">the number of times the record was attempted to be retrieved</param>
    /// <param name="exception">the actual exception that was thrown by the operation</param>
    /// <param name="ct">cancellation token</param>
    public virtual Task OnGetNextEventRecordError<TEvent>(string subscriberID,
                                                          int attemptCount,
                                                          Exception exception,
                                                          CancellationToken ct) where TEvent : class, IEvent
        => Task.CompletedTask;

    /// <summary>
    /// this method is triggered when the storage provider has trouble marking an event record as complete.
    /// </summary>
    /// <typeparam name="TEvent">the type of the event</typeparam>
    /// <param name="record">the event storage record that was supposed to be marked complete</param>
    /// <param name="attemptCount">the number of times the record was attempted to be marked complete</param>
    /// <param name="exception">the actual exception that was thrown by the operation</param>
    /// <param name="ct">cancellation token</param>
    public virtual Task OnMarkEventAsCompleteError<TEvent>(IEventStorageRecord record,
                                                           int attemptCount,
                                                           Exception exception,
                                                           CancellationToken ct) where TEvent : class, IEvent
        => Task.CompletedTask;

    /// <summary>
    /// this method is triggered when the storage provider has trouble persisting an event record.
    /// </summary>
    /// <typeparam name="TEvent">the type of the event</typeparam>
    /// <param name="record">the event storage record that was supposed to be persisted</param>
    /// <param name="attemptCount">the number of times the record was attempted to be persisted</param>
    /// <param name="exception">the actual exception that was thrown by the operation</param>
    /// <param name="ct">cancellation token</param>
    public virtual Task OnStoreEventRecordError<TEvent>(IEventStorageRecord record,
                                                        int attemptCount,
                                                        Exception exception,
                                                        CancellationToken ct) where TEvent : class, IEvent
        => Task.CompletedTask;

    /// <summary>
    /// this method is triggered when the default in-memory storage provider's internal queue for the given event type has been stagnant and in an overflow state.
    /// </summary>
    /// <typeparam name="TEvent">the type of the event</typeparam>
    /// <param name="record">the event storage record that was supposed to be added to the queue</param>
    /// <param name="ct">cancellation token</param>
    public virtual Task OnInMemoryQueueOverflow<TEvent>(IEventStorageRecord record,
                                                        CancellationToken ct) where TEvent : class, IEvent
        => Task.CompletedTask;
}