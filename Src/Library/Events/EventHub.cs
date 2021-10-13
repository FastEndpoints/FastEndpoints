namespace FastEndpoints;

/// <summary>
/// event notification hub which uses an in-process pub/sub messaging system based on .net events
/// </summary>
/// <typeparam name="TEvent">the type of notification event to publish or subscribe to</typeparam>
internal static class Event<TEvent> where TEvent : class
{
    internal static event AsyncEventHandler<TEvent>? OnReceived;

    internal static Task PublishAsync(TEvent eventModel, Mode waitMode = Mode.WaitForAll, CancellationToken cancellation = default)
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

