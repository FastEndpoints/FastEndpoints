namespace FastEndpoints;

/// <summary>
/// event notification hub which uses an in-process pub/sub messaging system based on .net events
/// </summary>
/// <typeparam name="TEvent">the type of notification event</typeparam>
public static class Event<TEvent> where TEvent : notnull
{
    internal static event AsyncEventHandler<TEvent>? OnReceived;

    /// <summary>
    /// publish the given model/dto to all the subscribers of the event notification
    /// </summary>
    /// <param name="eventModel">the notification event model/dto to publish</param>
    /// <param name="waitMode">specify whether to wait for none, any or all of the subscribers to complete their work</param>
    ///<param name="cancellation">an optional cancellation token</param>
    /// <returns>a Task that matches the wait mode specified.
    /// Mode.WaitForNone returns an already completed Task (fire and forget).
    /// Mode.WaitForAny returns a Task that will complete when any of the subscribers complete their work.
    /// Mode.WaitForAll return a Task that will complete only when all of the subscribers complete their work.</returns>
    public static Task PublishAsync(TEvent eventModel, Mode waitMode = Mode.WaitForAll, CancellationToken cancellation = default)
    {
        switch (waitMode)
        {
            case Mode.WaitForNone:

                _ = OnReceived?.InvokeAllAsync(eventModel, cancellation);
                return Task.CompletedTask;

            case Mode.WaitForAny:

                return OnReceived is null
                       ? Task.CompletedTask
                       : OnReceived.InvokeAnyAsync(eventModel, cancellation);

            case Mode.WaitForAll:

                return OnReceived is null
                       ? Task.CompletedTask
                       : OnReceived.InvokeAllAsync(eventModel, cancellation);

            default:
                return Task.CompletedTask;
        }
    }
}