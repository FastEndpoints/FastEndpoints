namespace FastEndpoints;

/// <summary>
/// provider-owned settings for in-process event delivery. configure through <c>UseMessaging</c> before publishing or warming messaging.
/// </summary>
public sealed class EventOptions
{
    const int InheritanceEnabled = 1;
    const int Frozen = 2;

    // A single CAS orders configuration against the first dispatch.
    int _state;

    /// <summary>
    /// include subscriptions for assignable base classes and interfaces using the published object's runtime type.
    /// defaults to false. changing the value after the first publish or warmup throws.
    /// </summary>
    public bool EnableInheritance
    {
        get => (Volatile.Read(ref _state) & InheritanceEnabled) != 0;
        set
        {
            var desired = value ? InheritanceEnabled : 0;
            while (true)
            {
                var state = Volatile.Read(ref _state);
                if ((state & InheritanceEnabled) == desired)
                    return;
                if ((state & Frozen) != 0)
                    throw new InvalidOperationException("Event inheritance must be configured before the first event publish or messaging warmup.");
                if (Interlocked.CompareExchange(ref _state, desired, state) == state)
                    return;
            }
        }
    }

    internal bool Freeze()
    {
        var state = Volatile.Read(ref _state);
        if ((state & Frozen) == 0)
            state = Interlocked.Or(ref _state, Frozen);

        return (state & InheritanceEnabled) != 0;
    }
}
