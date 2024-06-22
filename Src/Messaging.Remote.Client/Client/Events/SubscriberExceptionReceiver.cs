// ReSharper disable UnusedParameter.Global

namespace FastEndpoints;

/// <summary>
/// inherit this class and override it's methods in order to receive event subscriber exceptions.
/// </summary>
public abstract class SubscriberExceptionReceiver
{
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
    /// this method is triggered when there's an error reading the next event message from the underlying gRPC stream.
    /// you'd hardly ever be overriding this method since it's none of your business most of the time and the operation would be
    /// automatically retried until successful.
    /// </summary>
    /// <typeparam name="TEvent">the type of the event</typeparam>
    /// <param name="subscriberID">the unique ID of the subscriber</param>
    /// <param name="attemptCount">the number unsuccessful attempts to read the event message</param>
    /// <param name="exception">the actual exception that was thrown by the operation</param>
    /// <param name="ct">cancellation token</param>
    public virtual Task OnEventReceiveError<TEvent>(string subscriberID,
                                                    int attemptCount,
                                                    Exception exception,
                                                    CancellationToken ct) where TEvent : class, IEvent
        => Task.CompletedTask;

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
    /// this method is triggered when the event handler has trouble executing the <see cref="IEventHandler{TEvent}.HandleAsync(TEvent, CancellationToken)" /> method.
    /// </summary>
    /// <typeparam name="TEvent">the type of the event</typeparam>
    /// ///
    /// <typeparam name="THandler">the type of the event handler that failed to execute</typeparam>
    /// <param name="record">the event storage record that was supposed to be executed</param>
    /// <param name="attemptCount">the number of times the record was attempted to be executed</param>
    /// <param name="exception">the actual exception that was thrown by the operation</param>
    /// <param name="ct">cancellation token</param>
    public virtual Task OnHandlerExecutionError<TEvent, THandler>(IEventStorageRecord record,
                                                                  int attemptCount,
                                                                  Exception exception,
                                                                  CancellationToken ct)
        where TEvent : class, IEvent
        where THandler : IEventHandler<TEvent>
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
}