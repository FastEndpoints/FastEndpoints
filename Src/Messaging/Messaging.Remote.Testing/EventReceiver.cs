namespace FastEndpoints.Messaging.Remote.Testing;

class EventReceiver<TEvent> : IEventReceiver<TEvent> where TEvent : class, IEvent
{
    readonly List<TEvent> _received = [];

    void IEventReceiver<TEvent>.AddEvent(TEvent evnt)
        => _received.Add(evnt);

    public async Task<IEnumerable<TEvent>> WaitForMatchAsync(Func<TEvent, bool> match, int timeoutSeconds = 2, CancellationToken ct = default)
    {
        var start = DateTime.Now;

        while (!ct.IsCancellationRequested && DateTime.Now.Subtract(start).TotalSeconds < timeoutSeconds)
        {
            var res = _received.Where(match);

            if (res.Any())
                return res;

            await Task.Delay(100, CancellationToken.None);
        }

        return [];
    }
}