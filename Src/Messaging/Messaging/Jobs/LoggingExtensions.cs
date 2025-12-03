using Microsoft.Extensions.Logging;

namespace FastEndpoints.Messaging.Jobs;

static partial class LoggingExtensions
{
    [LoggerMessage(1, LogLevel.Error, "Job storage 'get-next-batch' error for [queue-id:{queueID}]({tCommand}): {msg}. Retrying in 5 seconds...")]
    public static partial void StorageRetrieveError(this ILogger l, string queueID, string tCommand, string msg);

    [LoggerMessage(2, LogLevel.Critical, "Job [{tCommand}] 'execution' error: [{msg}]")]
    public static partial void CommandExecutionCritical(this ILogger l, string tCommand, string msg);

    [LoggerMessage(3, LogLevel.Error, "Job storage 'on-execution-failure' error for [queue-id:{queueID}]({tCommand}): {msg}. Retrying in 5 seconds...")]
    public static partial void StorageOnExecutionFailureError(this ILogger l, string queueID, string tCommand, string msg);

    [LoggerMessage(4, LogLevel.Error, "Job storage 'mark-as-complete' error for [queue-id:{queueID}]({tCommand}): {msg}. Retrying in 5 seconds...")]
    public static partial void StorageMarkAsCompleteError(this ILogger l, string queueID, string tCommand, string msg);

    [LoggerMessage(5, LogLevel.Error, "Job storage 'store-job-result' error for [queue-id:{queueID}]({tCommand}): {msg}. Retrying in 5 seconds...")]
    public static partial void StorageStoreJobResultError(this ILogger l, string queueID, string tCommand, string msg);
}