using Microsoft.Extensions.Logging;

namespace FastEndpoints;

internal static partial class LoggingExtensions
{
    [LoggerMessage(1, LogLevel.Information, "Event subscriber registered! [id: {subscriberId}] ({tHandler}<{tEvent}>)")]
    public static partial void SubscriberRegistered(this ILogger l, string subscriberId, string tHandler, string tEvent);

    [LoggerMessage(2, LogLevel.Error, "Event storage 'create' error for [subscriber-id:{subscriberId}]({tEvent}): {msg}. Retrying in 5 seconds...")]
    public static partial void StorageCreateError(this ILogger l, string subscriberId, string tEvent, string msg);

    [LoggerMessage(3, LogLevel.Trace, "Event 'receive' error for [subscriber-id:{subscriberId}]({tEvent}): {msg}. Retrying in 5 seconds...")]
    public static partial void ReceiveTrace(this ILogger l, string subscriberId, string tEvent, string msg);

    [LoggerMessage(4, LogLevel.Information, " Remote connection configured!\r\n Remote Server: {address}\r\n Total Commands: {count}")]
    public static partial void RemoteConfigured(this ILogger l, string address, int count);

    [LoggerMessage(5, LogLevel.Information, "Event subscriber connected! [id:{subscriberId}]({tEvent})")]
    public static partial void SubscriberConnected(this ILogger l, string subscriberId, string tEvent);

    [LoggerMessage(6, LogLevel.Error, "Event storage 'retrieval' error for [subscriber-id:{subscriberId}]({tEvent}): {msg}. Retrying in 5 seconds...")]
    public static partial void StorageRetrieveError(this ILogger l, string subscriberId, string tEvent, string msg);

    [LoggerMessage(7, LogLevel.Error, "Event storage 'update' error for [subscriber-id:{subscriberId}]({tevent}): {msg}. Retrying in 5 seconds...")]
    public static partial void StorageUpdateError(this ILogger l, string subscriberId, string tEvent, string msg);

    [LoggerMessage(8, LogLevel.Warning, "Event queue for [subscriber-id:{subscriberId}]({tEvent}) is full! The subscriber has been removed from the broadcast list.")]
    public static partial void QueueOverflowWarning(this ILogger l, string subscriberId, string tEvent);
}