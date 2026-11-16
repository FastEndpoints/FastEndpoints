using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Grpc.Core;

namespace FastEndpoints;

interface IDuplexStreamCommandExecutor<in TCommand, TResult> : ICommandExecutor where TCommand : class where TResult : class
{
    IAsyncEnumerable<TResult> ExecuteDuplexStream(IAsyncEnumerable<TCommand> commands, CallOptions opts);
}

sealed class DuplexStreamCommandExecutor<TCommand, TResult>(ChannelBase channel, IRpcMarshallerFactory marshaller)
    : BaseCommandExecutor<TCommand, TResult>(channel: channel, methodType: MethodType.DuplexStreaming, marshaller: marshaller),
      IDuplexStreamCommandExecutor<TCommand, TResult>
    where TCommand : class, IDuplexStreamCommand<TResult>
    where TResult : class
{
    public IAsyncEnumerable<TResult> ExecuteDuplexStream(IAsyncEnumerable<TCommand> commands, CallOptions opts)
        => Execute(commands, opts);

    // the reader only replaces its error with the send fault when the error is the abort caused by that fault.
    // any other error (e.g. the connection dropping) is the call's real outcome.
    static bool IsAbort(Exception ex)
        => ex is OperationCanceledException or RpcException { StatusCode: StatusCode.Cancelled };

    // commands are sent by a background loop while the caller reads results, so neither side waits for the other to finish.
    // the response stream is the source of truth for the call's outcome. a failure on the sending side (the caller's command stream
    // throwing, or a write failing without ending the call, such as a serialization error) aborts the call and is rethrown to the reader.
    // note: with the channel's default retry policy, grpc-dotnet holds a failed write until the call commits (the server's first response),
    // so a write failure before that only surfaces then, or when the caller's cancellation/deadline ends the call.
    async IAsyncEnumerable<TResult> Execute(IAsyncEnumerable<TCommand> commands, CallOptions opts, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(opts.CancellationToken, ct);
        var token = cts.Token;
        using var call = StartCall();
        Exception? sendFault = null;
        var sending = SendAsync();

        try
        {
            while (true)
            {
                bool hasNext;

                try
                {
                    hasNext = await call.ResponseStream.MoveNext(CancellationToken.None); // the call's token governs cancellation
                }
                catch (Exception ex) when (sendFault is not null && IsAbort(ex))
                {
                    ExceptionDispatchInfo.Throw(sendFault);

                    throw;
                }

                if (!hasNext)
                    yield break;

                yield return call.ResponseStream.Current;
            }
        }
        finally
        {
            // stops the sender if the server ended the call or the caller stopped reading early.
            // the sender isn't awaited because a command stream that ignores cancellation would block the caller indefinitely.
            cts.Cancel();

            if (sending.IsCompleted)
                cts.Dispose();
            else
                _ = sending.ContinueWith(static (_, s) => ((CancellationTokenSource)s!).Dispose(), cts, TaskScheduler.Default);
        }

        // the finally above isn't reached if the call can't be started, so the linked token source is disposed here instead.
        AsyncDuplexStreamingCall<TCommand, TResult> StartCall()
        {
            try
            {
                return Invoker.AsyncDuplexStreamingCall(Method, null, opts.WithCancellationToken(token));
            }
            catch
            {
                cts.Dispose();

                throw;
            }
        }

        // never throws, so an abandoned sender can't cause an unobserved task exception.
        async Task SendAsync()
        {
            // ReSharper disable AccessToDisposedClosure

            try
            {
                await foreach (var cmd in commands.WithCancellation(token))
                {
                    try
                    {
                        await call.RequestStream.WriteAsync(cmd, token);
                    }
                    catch (Exception ex) when (ex is IOException or HttpRequestException or RpcException { StatusCode: StatusCode.OK or StatusCode.Cancelled })
                    {
                        // the call has ended or the transport failed. the call's status is reported through the response stream.
                        // a write to a call the server already ended throws 'Cancelled'. any other status (e.g. 'ResourceExhausted' for
                        // an item over the max send size) can leave the call open, so it takes the abort path below.
                        return;
                    }
                }

                await call.RequestStream.CompleteAsync();
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested)
                    return; //the reader is done or the caller canceled.

                sendFault = ex;
                cts.Cancel(); //aborts the call so the reader stops waiting and surfaces the fault
            }

            // ReSharper restore AccessToDisposedClosure
        }
    }
}