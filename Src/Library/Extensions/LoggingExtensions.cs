using Microsoft.Extensions.Logging;

namespace FastEndpoints;

static partial class LoggingExtensions
{
    [LoggerMessage(1, LogLevel.Information, "Registered {total} endpoints in {time} milliseconds.")]
    public static partial void EndpointsRegistered(this ILogger l, int total, string time);

    [LoggerMessage(2, LogLevel.Error, "The route [{route}] has {endpoints} endpoints registered to handle requests!")]
    public static partial void MultipleEndpointsRegisteredForRoute(this ILogger l, string route, int endpoints);

    [LoggerMessage(3, LogLevel.Error, "[{@exceptionType}] at [{@route}] due to [{@reason}]")]
    public static partial void LogStructuredException(this ILogger l, Exception ex, string? exceptionType, string? route, string? reason);

    [LoggerMessage(4, LogLevel.Error, """
                                     =================================
                                     {route}
                                     TYPE: {exceptionType}
                                     REASON: {reason}
                                     ---------------------------------
                                     {stackTrace}
                                     """)]
    public static partial void LogUnStructuredException(this ILogger l, string? exceptionType, string? route, string? reason, string? stackTrace);
}
