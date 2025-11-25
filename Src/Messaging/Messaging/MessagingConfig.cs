global using MsgCfg = FastEndpoints.MessagingConfig;

namespace FastEndpoints;

/// <summary>
/// global configuration settings for FastEndpoints Messaging
/// </summary>
public sealed class MessagingConfig
{
    static IMessagingServiceResolver? _resolver;

    internal static bool ResolverIsNotSet => _resolver is null;

    internal static IMessagingServiceResolver ServiceResolver
    {
        get => _resolver ?? throw new InvalidOperationException("Service resolver is null! Have you called AddMessaging() or configured the unit test environment correctly?");
        set => _resolver = value;
    }
}
