namespace FastEndpoints;

/// <summary>
/// interface for an event receiver that can be used to test the receipt of events in testing.
/// </summary>
/// <typeparam name="TEvent">the type of the event</typeparam>
public interface IEventReceiver<TEvent> where TEvent : IEvent
{
    /// <summary />
    protected internal void AddEvent(TEvent evnt);

    /// <summary>
    /// waits until at least one matching event is received not exceeding the timeout period.
    /// </summary>
    /// <param name="match">a predicate for matching events that should be returned by the method</param>
    /// <param name="timeoutSeconds">how long the method will wait until a matching event is received</param>
    /// <param name="ct">optional cancellation token</param>
    Task<IEnumerable<TEvent>> WaitForMatchAsync(Func<TEvent, bool> match, int timeoutSeconds = 1, CancellationToken ct = default);
}