using FastEndpoints.Messaging.Remote;
using FastEndpoints.Messaging.Remote.Core;
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
    internal async Task RetryUntilSuccess(Func<ValueTask> operation, Func<int, Exception, Task?>? onError, Action<string> logError, TimeSpan retryDelay, CancellationToken ct)
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

    internal async Task<List<TStorageRecord>> GetNextBatch<TEvent, TStorageRecord, TStorageProvider>(TStorageProvider storage,
                                                                                                  string subscriberID,
                                                                                                  int limit,
                                                                                                  CancellationToken ct,
                                                                                                  TimeSpan? retryDelay = null)
        where TEvent : class, IEvent
        where TStorageRecord : class, IEventStorageRecord, new()
        where TStorageProvider : IEventHubStorageProvider<TStorageRecord>
    {
        var errorCount = 0;
        var ctx = this;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                return (await storage.GetNextBatchAsync(
                            new()
                            {
                                CancellationToken = ct,
                                EventType = ctx.EventTypeName,
                                Limit = limit,
                                SubscriberID = subscriberID,
                                Match = e => e.SubscriberID == subscriberID &&
                                             e.EventType == ctx.EventTypeName &&
                                             !e.IsComplete &&
                                             DateTime.UtcNow <= e.ExpireOn
                            })).ToList();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                errorCount++;
                await InvokeExceptionReceiverSafely(() => ctx.Errors?.OnGetNextBatchError<TEvent>(subscriberID, errorCount, ex, ct));
                Logger.StorageGetNextBatchError(subscriberID, EventTypeName, ex.Message);

                if (!ct.IsCancellationRequested)
                    await Task.Delay(retryDelay ?? EventHubSettings.StorageRetryDelay);
            }
        }
    }

    internal async Task<List<TStorageRecord>?> GetNextNonEmptyBatch<TEvent, TStorageRecord, TStorageProvider>(TStorageProvider storage,
                                                                                                          string subscriberID,
                                                                                                          int limit,
                                                                                                          SemaphoreSlim subscriberSem,
                                                                                                          CancellationTokenSource cts,
                                                                                                          TimeSpan? retryDelay = null)
        where TEvent : class, IEvent
        where TStorageRecord : class, IEventStorageRecord, new()
        where TStorageProvider : IEventHubStorageProvider<TStorageRecord>
    {
        while (!cts.Token.IsCancellationRequested)
        {
            List<TStorageRecord> records;

            try
            {
                records = await GetNextBatch<TEvent, TStorageRecord, TStorageProvider>(storage, subscriberID, limit, cts.Token, retryDelay);
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                return null;
            }

            if (records.Count > 0)
                return records;

            await WaitForSignal(subscriberSem, cts);
        }

        return null;
    }

    internal Task MarkEventComplete<TEvent, TStorageRecord, TStorageProvider>(TStorageProvider storage,
                                                                            TStorageRecord record,
                                                                            string subscriberID,
                                                                            CancellationToken ct)
        where TEvent : class, IEvent
        where TStorageRecord : class, IEventStorageRecord, new()
        where TStorageProvider : IEventHubStorageProvider<TStorageRecord>
        => MarkEventComplete<TEvent, TStorageRecord, TStorageProvider>(storage, HubStorageBehavior.Durable, record, subscriberID, ct);

    internal async Task MarkEventComplete<TEvent, TStorageRecord, TStorageProvider>(TStorageProvider storage,
                                                                                  HubStorageBehavior storageBehavior,
                                                                                  TStorageRecord record,
                                                                                  string subscriberID,
                                                                                  CancellationToken ct)
        where TEvent : class, IEvent
        where TStorageRecord : class, IEventStorageRecord, new()
        where TStorageProvider : IEventHubStorageProvider<TStorageRecord>
    {
        if (!storageBehavior.ShouldMarkComplete)
            return;

        record.IsComplete = true;
        var ctx = this;

        // Use the linked connection/app token so an interrupted update leaves the durable row pending.
        await RetryUntilSuccess(
            operation: () => storage.MarkEventAsCompleteAsync(record, ct),
            onError: (count, ex) => ctx.Errors?.OnMarkEventAsCompleteError<TEvent>(record, count, ex, ct),
            logError: msg => ctx.Logger.StorageMarkAsCompleteError(subscriberID, ctx.EventTypeName, msg),
            retryDelay: EventHubSettings.StorageRetryDelay,
            ct: ct);
    }

    internal static async Task WaitForSignal(SemaphoreSlim subscriberSem, CancellationTokenSource cts)
    {
        try
        {
            if (await subscriberSem.WaitAsync(EventHubSettings.WaitForSignalTimeout, cts.Token)) //wait for poll interval, semaphore release, or shutdown.
                while (subscriberSem.Wait(0)) { }                                                //drain residual releases so the next poll only runs after new work arrives.
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            //let the main loop exit so the caller can release the connection.
        }
        catch (ObjectDisposedException)
        {
            cts.Cancel();
        }
    }

    internal async ValueTask<TEvent?> DeserializeEvent<TEvent>(IEventStorageRecord record, CancellationToken ct, TimeSpan? retryDelay = null)
        where TEvent : class, IEvent
    {
        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                return record.GetEvent<TEvent>() ?? throw new InvalidOperationException("The stored event payload is null.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                ct.ThrowIfCancellationRequested();

                if (attempt < EventHubSettings.DeserializationAttempts)
                {
                    await Task.Delay(retryDelay ?? EventHubSettings.DeserializationRetryDelay, ct);

                    continue;
                }

                Logger.DeserializeEventError(ex, record.TrackingID, record.SubscriberID, EventTypeName, attempt);
                var errors = Errors;
                await InvokeExceptionReceiverSafely(() => errors?.OnDeserializeEventError<TEvent>(record, attempt, ex, ct));
                ct.ThrowIfCancellationRequested();

                return null;
            }
        }
    }

    /// <summary>
    /// safely invokes a user-provided exception receiver callback. exceptions are logged without stopping the worker.
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