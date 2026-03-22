using FastEndpoints.Messaging.Remote;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

/// <summary>
/// captures the common context (logger, exception receiver, event type name, app cancellation) shared across
/// all retry and error-receiver invocations within the event hub, eliminating the need to pass these values
/// repeatedly at every call site.
/// </summary>
readonly struct HubContext
{
    internal ILogger Logger { get; }
    internal EventHubExceptionReceiver? Errors { get; }
    internal string EventTypeName { get; }
    internal CancellationToken AppCancellation { get; }

    internal HubContext(ILogger logger, EventHubExceptionReceiver? errors, string eventTypeName, CancellationToken appCancellation)
    {
        Logger = logger;
        Errors = errors;
        EventTypeName = eventTypeName;
        AppCancellation = appCancellation;
    }

    /// <summary>
    /// retries <paramref name="operation" /> in a loop until it succeeds or <paramref name="ct" /> is canceled.
    /// on each failure the error callback is invoked safely (exceptions from user code are caught),
    /// the error is logged, and execution is delayed before the next attempt.
    /// </summary>
    internal async Task RetryUntilSuccess(Func<ValueTask> operation,
                                          Func<int, Exception, Task?>? onError,
                                          Action<string> logError,
                                          TimeSpan retryDelay,
                                          CancellationToken ct)
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
                await InvokeExceptionReceiverSafely(() => onError?.Invoke(errorCount, ex));
                logError(ex.Message);

                if (ct.IsCancellationRequested)
                    return;

                await Task.Delay(retryDelay, CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// safely invokes a user-provided exception receiver callback. any exception thrown by user code is logged but never propagated,
    /// ensuring a faulty receiver can never tear down the event hub worker.
    /// </summary>
    internal async Task InvokeExceptionReceiverSafely(Func<Task?> callbackFactory)
    {
        try
        {
            var callback = callbackFactory();

            if (callback is null)
                return;

            await callback;
        }
        catch (Exception ex)
        {
            Logger.EventHubExceptionReceiverFault(ex, EventTypeName);
        }
    }
}
