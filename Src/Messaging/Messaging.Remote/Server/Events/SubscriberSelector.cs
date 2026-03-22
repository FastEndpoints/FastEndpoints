namespace FastEndpoints;

interface ISubscriberSelector
{
    string[] SelectRecipients(SubscriberRegistry registry);
}

sealed class FanOutSelector : ISubscriberSelector
{
    public string[] SelectRecipients(SubscriberRegistry registry)
    {
        registry.PruneStale();

        return registry.GetAllSubscriberIds();
    }
}

sealed class RoundRobinSelector : ISubscriberSelector
{
    readonly Lock _lock = new();
    string? _lastReceivedBy;

    public string[] SelectRecipients(SubscriberRegistry registry)
    {
        registry.PruneStale();

        var connectedSubIds = registry.GetConnectedSubscriberIds();

        if (connectedSubIds.Length <= 1)
            return connectedSubIds;

        return [GetNextRoundRobinSubscriberId(connectedSubIds)];
    }

    //internal to allow unit testing
    internal string GetNextRoundRobinSubscriberId(string[] connectedSubIds)
    {
        lock (_lock)
        {
            var lastIndex = Array.IndexOf(connectedSubIds, _lastReceivedBy);
            var nextIndex = (lastIndex + 1) % connectedSubIds.Length;
            var nextSubscriberId = connectedSubIds[nextIndex];
            _lastReceivedBy = nextSubscriberId;

            return nextSubscriberId;
        }
    }
}