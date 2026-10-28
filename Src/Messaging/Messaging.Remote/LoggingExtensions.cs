using Microsoft.Extensions.Logging;

namespace FastEndpoints.Messaging.Remote;

static partial class LoggingExtensions
{
    [LoggerMessage(1, LogLevel.Error, "Storage provider failed to restore Subscriber IDs for [{tEvent}]. Retrying in 5 seconds...")]
    public static partial void RestoreSubscriberIDsError(this ILogger l, string tEvent);

    [LoggerMessage(2, LogLevel.Information, "Event subscriber connected! [id:{subscriberId}]({tEvent})")]
    public static partial void SubscriberConnected(this ILogger l, string subscriberId, string tEvent);

    [LoggerMessage(3, LogLevel.Warning, "No event subscribers to connect for: [{tEvent}]")]
    public static partial void NoSubscribersWarning(this ILogger l, string tEvent);

    [LoggerMessage(
        4,
        LogLevel.Warning,
        "Event queue for [subscriber-id:{subscriberId}]({tEvent}) is full! The subscriber has been removed from the broadcast list.")]
    public static partial void QueueOverflowWarning(this ILogger l, string subscriberId, string tEvent);

    [LoggerMessage(5, LogLevel.Error, "Event hub exception receiver fault during operation for ({tEvent}).")]
    public static partial void EventHubExceptionReceiverFault(this ILogger l, Exception ex, string tEvent);

    [LoggerMessage(6, LogLevel.Warning, "Event subscriber connection rejected because one is already open! [id:{subscriberId}]({tEvent})")]
    public static partial void SubscriberAlreadyConnected(this ILogger l, string subscriberId, string tEvent);

    [LoggerMessage(7, LogLevel.Warning, "Event delivery acknowledgement tracking id did not match the delivery for [subscriber-id:{subscriberId}]({tEvent}).")]
    public static partial void DeliveryAckMismatch(this ILogger l, string subscriberId, string tEvent);

    [LoggerMessage(8, LogLevel.Warning, "Event delivery has an empty tracking id or no event for [subscriber-id:{subscriberId}]({tEvent}).")]
    public static partial void EmptyDeliveryTrackingId(this ILogger l, string subscriberId, string tEvent);

    [LoggerMessage(9, LogLevel.Error, "Event deserialization failed after {attemptCount} attempts. Completing undelivered event [tracking-id:{trackingId}] [subscriber-id:{subscriberId}]({tEvent}). Recovery requires OnDeserializeEventError.")]
    public static partial void DeserializeEventError(this ILogger l, Exception ex, Guid trackingId, string subscriberId, string tEvent, int attemptCount);

    [LoggerMessage(10, LogLevel.Warning, "Event delivery acknowledgement timed out. Closing subscription with pending event [tracking-id:{trackingId}] [subscriber-id:{subscriberId}]({tEvent}).")]
    public static partial void DeliveryAckTimeout(this ILogger l, Guid trackingId, string subscriberId, string tEvent);
}