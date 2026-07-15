using Microsoft.Extensions.Logging;

namespace FastEndpoints.Messaging.Remote.Reflection;

static partial class LoggingExtensions
{
    [LoggerMessage(
        1,
        LogLevel.Warning,
        "Command [{tCommand}] cannot be described for gRPC reflection and will not be listed: {reason}. " +
        "The handler itself is unaffected and still executes normally.")]
    public static partial void CommandNotDescribable(this ILogger l, string tCommand, string reason);
}
