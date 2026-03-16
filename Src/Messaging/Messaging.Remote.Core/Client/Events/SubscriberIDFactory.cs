namespace FastEndpoints;

static class SubscriberIDFactory
{
    internal static string Create(string? explicitSubscriberID, string clientIdentifier, Type subscriberType, string channelTarget)
        => explicitSubscriberID is not null
               ? Normalize(explicitSubscriberID)
               : (Environment.MachineName + subscriberType.FullName + channelTarget + clientIdentifier).ToHash();

    internal static string Normalize(string subscriberID)
        => string.IsNullOrWhiteSpace(subscriberID)
               ? throw new ArgumentException("Subscriber ID cannot be null, empty, or whitespace.", nameof(subscriberID))
               : subscriberID.Trim();
}
