using Microsoft.Extensions.Logging;

namespace FastEndpoints.Messaging.Remote.Core;

static partial class LoggingExtensions
{
    [LoggerMessage(4, LogLevel.Information, " Remote connection configured!\r\n Remote Server: {address}\r\n Total Commands: {count}")]
    public static partial void RemoteConfigured(this ILogger l, string address, int count);
}