namespace FastEndpoints;

public static class EventExtensions
{
    /// <summary>
    /// publish the event to all subscribers registered to handle this type of event.
    /// </summary>
    /// <typeparam name="TEvent">the type of the event model</typeparam>
    /// <param name="eventModel">the notification event model/dto to publish</param>
    /// <param name="waitMode">specify whether to wait for none, any or all of the subscribers to complete their work</param>
    ///<param name="cancellation">an optional cancellation token</param>
    /// <returns>a Task that matches the wait mode specified.
    /// <see cref="Mode.WaitForNone"/> returns an already completed Task (fire and forget).
    /// <see cref="Mode.WaitForAny"/> returns a Task that will complete when any of the subscribers complete their work.
    /// <see cref="Mode.WaitForAll"/> return a Task that will complete only when all of the subscribers complete their work.</returns>
    public static Task PublishAsync<TEvent>(this TEvent eventModel, Mode waitMode = Mode.WaitForAll, CancellationToken cancellation = default) where TEvent : IEvent
        => Config.ServiceResolver.Resolve<Event<TEvent>>().PublishAsync(eventModel, waitMode, cancellation);
}