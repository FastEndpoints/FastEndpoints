using Microsoft.Extensions.Logging;

namespace FastEndpoints.Messaging.Remote.Core;

static partial class LoggingExtensions
{
    [LoggerMessage(1, LogLevel.Information, "Remote connection configured!\r\n Remote Server: {address}\r\n Total Commands: {count}")]
    public static partial void RemoteConnectionConfigured(this ILogger l, string address, int count);

    [LoggerMessage(2, LogLevel.Information, "Event subscriber registered! [id: {subscriberId}] ({tHandler}<{tEvent}>)")]
    public static partial void SubscriberRegistered(this ILogger l, string subscriberId, string tHandler, string tEvent);

    [LoggerMessage(3, LogLevel.Critical, "Event [{tEvent}] 'set-event' (serialization) error: {msg}")]
    public static partial void SetEventCritical(this ILogger l, string tEvent, string msg);

    [LoggerMessage(4, LogLevel.Error, "Event subscriber storage 'store-event' error for [subscriber-id:{subscriberId}]({tEvent}): {msg}. Retrying in 5 seconds...")]
    public static partial void StoreEventError(this ILogger l, string subscriberId, string tEvent, string msg);

    [LoggerMessage(5, LogLevel.Error, "Event hub storage 'store-events' error for ({tEvent}): {msg}. Retrying in 5 seconds...")]
    public static partial void StoreEventsError(this ILogger l, string tEvent, string msg);

    [LoggerMessage(6, LogLevel.Trace, "Event 'stream-receive' error for [subscriber-id:{subscriberId}]({tEvent}): {msg}. Retrying in 5 seconds...")]
    public static partial void StreamReceiveTrace(this ILogger l, string subscriberId, string tEvent, string msg);

    [LoggerMessage(7, LogLevel.Error, "Event storage 'get-next-batch' error for [subscriber-id:{subscriberId}]({tEvent}): {msg}. Retrying in 5 seconds...")]
    public static partial void StorageGetNextBatchError(this ILogger l, string subscriberId, string tEvent, string msg);

    [LoggerMessage(8, LogLevel.Critical, "Event [{tEvent}] 'handler-execution' error: [{msg}]. Retrying after 5 seconds...")]
    public static partial void HandlerExecutionCritical(this ILogger l, string tEvent, string msg);

    [LoggerMessage(9, LogLevel.Error, "Event storage 'mark-as-complete' error for [subscriber-id:{subscriberId}]({tEvent}): {msg}. Retrying in 5 seconds...")]
    public static partial void StorageMarkAsCompleteError(this ILogger l, string subscriberId, string tEvent, string msg);

    [LoggerMessage(10, LogLevel.Critical, "Event receiver task terminated unexpectedly for [subscriber-id:{subscriberId}]({tEvent}): {msg}")]
    public static partial void EventReceiverTaskTerminatedCritical(this ILogger l, string subscriberId, string tEvent, string msg);

    [LoggerMessage(11, LogLevel.Critical, "Event executor task terminated unexpectedly for [subscriber-id:{subscriberId}]({tEvent}): {msg}")]
    public static partial void EventExecutorTaskTerminatedCritical(this ILogger l, string subscriberId, string tEvent, string msg);

    [LoggerMessage(12, LogLevel.Critical, "Unexpected fault while observing event execution completion for [subscriber-id:{subscriberId}]({eventType}).")]
    public static partial void EventExecutionCompletionObservedCritical(this ILogger l, Exception ex, string subscriberId, string eventType);

    [LoggerMessage(13, LogLevel.Critical, "Unexpected fault while executing event for [subscriber-id:{subscriberId}]({eventType}).")]
    public static partial void EventExecutionTaskCritical(this ILogger l, Exception ex, string subscriberId, string eventType);

    [LoggerMessage(14, LogLevel.Warning,
        "Event record with empty TrackingID for [subscriber-id:{subscriberId}]({tEvent}). " +
        "Custom storage providers must persist and restore TrackingID to ensure correct concurrency behavior.")]
    public static partial void EmptyTrackingIdWarning(this ILogger l, string subscriberId, string tEvent);

    [LoggerMessage(15, LogLevel.Error, "Subscriber exception receiver fault during '{operation}' for [subscriber-id:{subscriberId}]({eventType}).")]
    public static partial void SubscriberExceptionReceiverFault(this ILogger l, Exception ex, string operation, string subscriberId, string eventType);
}
