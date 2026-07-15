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

    [LoggerMessage(
        2,
        LogLevel.Error,
        "Unexpected failure while describing command [{tCommand}] for gRPC reflection. It will not be listed, but the handler " +
        "itself is unaffected and still executes normally. This is a bug - please report it.")]
    public static partial void CommandDescriptorFailure(this ILogger l, Exception ex, string tCommand);
}
