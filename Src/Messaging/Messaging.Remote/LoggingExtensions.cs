using Microsoft.Extensions.Logging;

namespace FastEndpoints.Messaging.Remote;

static partial class LoggingExtensions
{
    [LoggerMessage(2, LogLevel.Information, "Event subscriber connected! [id:{subscriberId}]({tEvent})")]
    public static partial void SubscriberConnected(this ILogger l, string subscriberId, string tEvent);

    [LoggerMessage(3, LogLevel.Warning, "No event subscribers to connect for: [{tEvent}]")]
    public static partial void NoSubscribersWarning(this ILogger l, string tEvent);

    [LoggerMessage(6, LogLevel.Warning, "Event queue for [subscriber-id:{subscriberId}]({tEvent}) is full! The subscriber has been removed from the broadcast list.")]
    public static partial void QueueOverflowWarning(this ILogger l, string subscriberId, string tEvent);
}