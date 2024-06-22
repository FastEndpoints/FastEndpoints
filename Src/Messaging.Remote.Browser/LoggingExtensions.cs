using Microsoft.Extensions.Logging;

namespace FastEndpoints.Messaging.Remote;

static partial class LoggingExtensions
{
    [LoggerMessage(1, LogLevel.Information, "Event subscriber registered! [id: {subscriberId}] ({tHandler}<{tEvent}>)")]
    public static partial void SubscriberRegistered(this ILogger l, string subscriberId, string tHandler, string tEvent);

    [LoggerMessage(2, LogLevel.Information, "Event subscriber connected! [id:{subscriberId}]({tEvent})")]
    public static partial void SubscriberConnected(this ILogger l, string subscriberId, string tEvent);

    [LoggerMessage(3, LogLevel.Warning, "No event subscribers to connect for: [{tEvent}]")]
    public static partial void NoSubscribersWarning(this ILogger l, string tEvent);

    [LoggerMessage(4, LogLevel.Information, " Remote connection configured!\r\n Remote Server: {address}\r\n Total Commands: {count}")]
    public static partial void RemoteConfigured(this ILogger l, string address, int count);

    [LoggerMessage(5, LogLevel.Trace, "Event 'stream-receive' error for [subscriber-id:{subscriberId}]({tEvent}): {msg}. Retrying in 5 seconds...")]
    public static partial void StreamReceiveTrace(this ILogger l, string subscriberId, string tEvent, string msg);

    [LoggerMessage(6, LogLevel.Warning, "Event queue for [subscriber-id:{subscriberId}]({tEvent}) is full! The subscriber has been removed from the broadcast list.")]
    public static partial void QueueOverflowWarning(this ILogger l, string subscriberId, string tEvent);

    [LoggerMessage(7, LogLevel.Critical, "Event [{tEvent}] 'handler-execution' error: [{msg}]. Retrying after 5 seconds...")]
    public static partial void HandlerExecutionCritical(this ILogger l, string tEvent, string msg);

    [LoggerMessage(8, LogLevel.Error, "Event storage 'store-event' error for [subscriber-id:{subscriberId}]({tEvent}): {msg}. Retrying in 5 seconds...")]
    public static partial void StoreEventError(this ILogger l, string subscriberId, string tEvent, string msg);

    [LoggerMessage(9, LogLevel.Error, "Event storage 'get-next-batch' error for [subscriber-id:{subscriberId}]({tEvent}): {msg}. Retrying in 5 seconds...")]
    public static partial void StorageGetNextBatchError(this ILogger l, string subscriberId, string tEvent, string msg);

    [LoggerMessage(10, LogLevel.Error, "Event storage 'mark-as-complete' error for [subscriber-id:{subscriberId}]({tEvent}): {msg}. Retrying in 5 seconds...")]
    public static partial void StorageMarkAsCompleteError(this ILogger l, string subscriberId, string tEvent, string msg);
}