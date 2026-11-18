namespace FastEndpoints;

/// <summary>
/// interface for an event receiver that can be used to test the receipt of events in testing.
/// </summary>
/// <typeparam name="TEvent">the type of the event</typeparam>
public interface IEventReceiver<TEvent> where TEvent : notnull
{
    /// <summary />
    protected internal void AddEvent(TEvent evnt);

    /// <summary>
    /// waits until at least one matching event is received not exceeding the timeout period.
    /// </summary>
    /// <param name="match">a predicate for matching events that should be returned by the method</param>
    /// <param name="timeoutSeconds">how long the method will wait until a matching event is received. default value is 2 seconds</param>
    /// <param name="ct">optional cancellation token</param>
    Task<IEnumerable<TEvent>> WaitForMatchAsync(Func<TEvent, bool> match, int timeoutSeconds = 2, CancellationToken ct = default);
}

// Lets inherited dispatch record into receivers of every assignable type without per-type generic code.
interface IUntypedEventReceiver
{
    void AddEvent(object evnt);
}

/// <summary>
/// the default implementation of an event receiver that can be used to test the execution of and event.
/// </summary>
/// <typeparam name="TEvent">the type of the event</typeparam>
public sealed class EventReceiver<TEvent> : IEventReceiver<TEvent>, IUntypedEventReceiver where TEvent : notnull
{
    readonly List<TEvent> _received = [];

    void IEventReceiver<TEvent>.AddEvent(TEvent evnt)
    {
        lock (_received)
            _received.Add(evnt);
    }

    void IUntypedEventReceiver.AddEvent(object evnt)
        => ((IEventReceiver<TEvent>)this).AddEvent((TEvent)evnt);

    /// <inheritdoc />
    public async Task<IEnumerable<TEvent>> WaitForMatchAsync(Func<TEvent, bool> match, int timeoutSeconds = 2, CancellationToken ct = default)
    {
        var start = DateTime.Now;

        while (!ct.IsCancellationRequested && DateTime.Now.Subtract(start).TotalSeconds < timeoutSeconds)
        {
            // Publishers can add concurrently, so match against a snapshot and return a materialized result.
            TEvent[] snapshot;
            lock (_received)
                snapshot = [.. _received];

            var res = snapshot.Where(match).ToArray();

            if (res.Length > 0)
                return res;

            await Task.Delay(100, CancellationToken.None);
        }

        return [];
    }
}
