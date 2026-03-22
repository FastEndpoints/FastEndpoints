using FastEndpoints.Messaging.Remote.Core;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

/// <summary>
/// captures the common context (logger, subscriberID, eventTypeName) shared across all
/// retry and error-receiver invocations within a single worker method, eliminating the need
/// to pass these values repeatedly at every call site.
/// </summary>
readonly struct SubscriberContext
{
    internal ILogger Logger { get; }
    internal string SubscriberID { get; }
    internal string EventTypeName { get; }

    internal SubscriberContext(ILogger logger, string subscriberID, string eventTypeName)
    {
        Logger = logger;
        SubscriberID = subscriberID;
        EventTypeName = eventTypeName;
    }

    /// <summary>
    /// retries <paramref name="operation" /> in a loop until it succeeds or <paramref name="ct" /> is canceled.
    /// on each failure the error receiver callback is invoked safely (exceptions from user code are caught),
    /// the error is logged, and execution is delayed before the next attempt.
    /// </summary>
    internal async Task RetryUntilSuccess(Func<ValueTask> operation,
                                          Func<int, Exception, Task?>? onError,
                                          Action<string> logError,
                                          TimeSpan retryDelay,
                                          CancellationToken ct,
                                          string operationName)
    {
        var errorCount = 0;

        while (true)
        {
            try
            {
                await operation();

                return;
            }
            catch (Exception ex)
            {
                errorCount++;
                await InvokeExceptionReceiverSafely(() => onError?.Invoke(errorCount, ex), operationName);
                logError(ex.Message);

                if (ct.IsCancellationRequested)
                    return;

                await Task.Delay(retryDelay, ct);
            }
        }
    }

    /// <summary>
    /// safely invokes a user-provided exception receiver callback. any exception thrown by user code is logged but never propagated,
    /// ensuring a faulty receiver can never tear down the subscriber worker.
    /// </summary>
    internal async Task InvokeExceptionReceiverSafely(Func<Task?> callbackFactory, string operation)
    {
        try
        {
            var callback = callbackFactory();

            if (callback is null)
                return;

            // exception receiver hooks are user extension points. await them so any state changes they make are visible
            // to the retry loop, but never let a faulty callback tear down the subscriber worker.
            await callback;
        }
        catch (Exception ex)
        {
            Logger.SubscriberExceptionReceiverFault(ex, operation, SubscriberID, EventTypeName);
        }
    }
}