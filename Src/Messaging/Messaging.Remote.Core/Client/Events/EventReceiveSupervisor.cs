using FastEndpoints.Messaging.Remote.Core;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

enum ReceiveSessionResult
{
    Reconnect,
    Stop
}

sealed class EventReceiveSupervisor<TEvent>(SubscriberContext ctx, SubscriberExceptionReceiver? errors, TimeSpan retryInterval, CancellationToken ct) where TEvent : class, IEvent
{
    int _receiveErrorCount;

    internal void Received()
        => _receiveErrorCount = 0;

    internal async Task ReportErrorAsync(Exception ex)
    {
        _receiveErrorCount++;
        await ctx.InvokeExceptionReceiverSafely(
            () => errors?.OnEventReceiveError<TEvent>(ctx.SubscriberID, _receiveErrorCount, ex, ct),
            "stream-receive");
    }

    internal async Task RunAsync(Func<Task<ReceiveSessionResult>> receiveSession, Action? createReplacement = null)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (await receiveSession() == ReceiveSessionResult.Stop)
                        return;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    await ReportErrorAsync(ex);
                    ctx.Logger.StreamReceiveTrace(ctx.SubscriberID, ctx.EventTypeName, ex.Message);
                }

                await Task.Delay(retryInterval, ct);

                // Ordinary replacement creation retains its terminal exception boundary.
                createReplacement?.Invoke();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            //graceful shutdown. cancellation is expected.
        }
        catch (Exception ex)
        {
            ctx.Logger.EventReceiverTaskTerminatedCritical(ctx.SubscriberID, ctx.EventTypeName, ex.Message);
        }
    }
}