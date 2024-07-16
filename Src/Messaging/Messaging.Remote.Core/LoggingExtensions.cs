using Microsoft.Extensions.Logging;

namespace FastEndpoints.Messaging.Remote.Core;

static partial class LoggingExtensions
{
    [LoggerMessage(4, LogLevel.Information, " Remote connection configured!\r\n Remote Server: {address}\r\n Total Commands: {count}")]
    public static partial void RemoteConfigured(this ILogger l, string address, int count);

    [LoggerMessage(1, LogLevel.Information, "Event subscriber registered! [id: {subscriberId}] ({tHandler}<{tEvent}>)")]
    public static partial void SubscriberRegistered(this ILogger l, string subscriberId, string tHandler, string tEvent);

    [LoggerMessage(8, LogLevel.Error, "Event storage 'store-event' error for [subscriber-id:{subscriberId}]({tEvent}): {msg}. Retrying in 5 seconds...")]
    public static partial void StoreEventError(this ILogger l, string subscriberId, string tEvent, string msg);

    [LoggerMessage(5, LogLevel.Trace, "Event 'stream-receive' error for [subscriber-id:{subscriberId}]({tEvent}): {msg}. Retrying in 5 seconds...")]
    public static partial void StreamReceiveTrace(this ILogger l, string subscriberId, string tEvent, string msg);

    [LoggerMessage(9, LogLevel.Error, "Event storage 'get-next-batch' error for [subscriber-id:{subscriberId}]({tEvent}): {msg}. Retrying in 5 seconds...")]
    public static partial void StorageGetNextBatchError(this ILogger l, string subscriberId, string tEvent, string msg);

    [LoggerMessage(7, LogLevel.Critical, "Event [{tEvent}] 'handler-execution' error: [{msg}]. Retrying after 5 seconds...")]
    public static partial void HandlerExecutionCritical(this ILogger l, string tEvent, string msg);

    [LoggerMessage(10, LogLevel.Error, "Event storage 'mark-as-complete' error for [subscriber-id:{subscriberId}]({tEvent}): {msg}. Retrying in 5 seconds...")]
    public static partial void StorageMarkAsCompleteError(this ILogger l, string subscriberId, string tEvent, string msg);
}