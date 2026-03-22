using System.Collections.Concurrent;

namespace FastEndpoints;

sealed class SubscriberRegistry
{
    //key: subscriber id
    //val: subscriber object
    internal readonly ConcurrentDictionary<string, Subscriber> Subscribers = new();

    readonly Lock _configLock = new();
    readonly TimeSpan _subscriberRetention = TimeSpan.FromHours(24);
    HashSet<string> _knownSubscriberIDs = [];

    internal void Configure(IEnumerable<string>? knownSubscriberIDs)
    {
        var configuredSubscriberIDs = new HashSet<string>(
            knownSubscriberIDs?.Select(SubscriberIDFactory.Normalize).Distinct(StringComparer.Ordinal) ?? [],
            StringComparer.Ordinal);

        lock (_configLock)
        {
            _knownSubscriberIDs = configuredSubscriberIDs;

            foreach (var subId in _knownSubscriberIDs)
            {
                Subscribers.AddOrUpdate(
                    subId,
                    _ => new() { IsKnownSubscriber = true },
                    (_, existing) => existing with { IsKnownSubscriber = true });
            }

            // if a subscriber was previously part of the configured set but is no longer listed, downgrade it to a
            // normal subscriber so stale config doesn't keep it pinned in the protected configured state forever.
            foreach (var kv in Subscribers.Where(kv => !_knownSubscriberIDs.Contains(kv.Key) && kv.Value.IsKnownSubscriber).ToArray())
            {
                while (Subscribers.TryGetValue(kv.Key, out var current) && current.IsKnownSubscriber)
                {
                    if (Subscribers.TryUpdate(kv.Key, current with { IsKnownSubscriber = false }, current))
                        break;
                }
            }
        }
    }

    internal Subscriber RegisterConnection(string subscriberID)
        => Subscribers.AddOrUpdate(
            subscriberID,
            _ => new()
            {
                ConnectionCount = 1,
                LastSeenUtc = DateTime.UtcNow,
                IsKnownSubscriber = _knownSubscriberIDs.Contains(subscriberID)
            },
            (_, existing) => existing with
            {
                ConnectionCount = existing.ConnectionCount + 1,
                LastSeenUtc = DateTime.UtcNow,
                IsKnownSubscriber = existing.IsKnownSubscriber || _knownSubscriberIDs.Contains(subscriberID)
            });

    internal void ReleaseConnection(string subscriberID)
        => TryUpdate(
            subscriberID,
            s => s with
            {
                ConnectionCount = Math.Max(0, s.ConnectionCount - 1),
                LastSeenUtc = DateTime.UtcNow
            });

    internal void RestoreSubscriber(string subscriberID)
    {
        Subscribers.AddOrUpdate(
            subscriberID,
            _ => new() { LastSeenUtc = DateTime.UtcNow, IsKnownSubscriber = _knownSubscriberIDs.Contains(subscriberID) },
            (_, existing) => existing with
            {
                LastSeenUtc = DateTime.UtcNow,
                IsKnownSubscriber = existing.IsKnownSubscriber || _knownSubscriberIDs.Contains(subscriberID)
            });
    }

    internal void Remove(string subscriberID, bool allowConfiguredRemoval)
    {
        while (Subscribers.TryGetValue(subscriberID, out var current))
        {
            if (current.IsKnownSubscriber && !allowConfiguredRemoval)
                return;

            if (!Subscribers.TryRemove(new(subscriberID, current)))
                continue;

            current.Sem.Dispose();

            return;
        }
    }

    internal void PruneStale()
    {
        var staleCutoff = DateTime.UtcNow.Subtract(_subscriberRetention);

        foreach (var kv in Subscribers.Where(kv => kv.Value is { IsKnownSubscriber: false, ConnectionCount: 0 } && kv.Value.LastSeenUtc <= staleCutoff).ToArray())
        {
            //remove only if the entry is still the same stale snapshot we inspected above.
            //this avoids pruning a subscriber that reconnected or was otherwise updated after the snapshot was taken.
            if (Subscribers.TryRemove(new(kv.Key, kv.Value)))
                kv.Value.Sem.Dispose();
        }
    }

    internal string[] GetAllSubscriberIds()
        => Subscribers.Keys.ToArray();

    internal string[] GetConnectedSubscriberIds()
        => Subscribers
           .Where(kv => kv.Value.ConnectionCount > 0)
           .Select(kv => kv.Key)
           .OrderBy(id => id, StringComparer.Ordinal)
           .ToArray();

    internal void SignalSubscriber(string subscriberID)
    {
        if (!Subscribers.TryGetValue(subscriberID, out var subscriber))
            return;

        try
        {
            subscriber.Sem.Release();
        }
        catch (ObjectDisposedException)
        {
            //subscriber was removed after persistence completed. event will be picked up on reconnect if needed.
        }
    }

    Subscriber? TryUpdate(string subscriberID, Func<Subscriber, Subscriber> update)
    {
        while (true)
        {
            if (!Subscribers.TryGetValue(subscriberID, out var current))
                return null;

            var updated = update(current);

            if (Subscribers.TryUpdate(subscriberID, updated, current))
                return updated;
        }
    }
}
