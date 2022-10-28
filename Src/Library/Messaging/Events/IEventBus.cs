namespace FastEndpoints;

/// <summary>
/// interface to be implemented by an event bus
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// publishes a given event model to all subscribers registered to handle the that type of event.
    /// </summary>
    /// <typeparam name="TEvent">the type of the event model</typeparam>
    /// <param name="eventModel">the notification event model/dto to publish</param>
    /// <param name="waitMode">specify whether to wait for none, any or all of the subscribers to complete their work</param>
    ///<param name="cancellation">an optional cancellation token</param>
    /// <returns>a Task that matches the wait mode specified.
    /// <see cref="Mode.WaitForNone"/> returns an already completed Task (fire and forget).
    /// <see cref="Mode.WaitForAny"/> returns a Task that will complete when any of the subscribers complete their work.
    /// <see cref="Mode.WaitForAll"/> return a Task that will complete only when all of the subscribers complete their work.</returns>
    Task PublishAsync<TEvent>(TEvent eventModel, Mode waitMode = Mode.WaitForAll, CancellationToken cancellation = default) where TEvent : notnull;
}