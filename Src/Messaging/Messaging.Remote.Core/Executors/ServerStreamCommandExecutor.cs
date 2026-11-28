using System.Runtime.CompilerServices;
using Grpc.Core;
using Grpc.Net.Client;

namespace FastEndpoints;

interface IServerStreamCommandExecutor<TResult> : ICommandExecutor where TResult : class
{
    IAsyncEnumerable<TResult> ExecuteServerStream(IServerStreamCommand<TResult> command, CallOptions opts);
}

sealed class ServerStreamCommandExecutor<TCommand, TResult>(GrpcChannel channel, IRpcMarshallerFactory marshaller)
    : BaseCommandExecutor<TCommand, TResult>(channel: channel, methodType: MethodType.ServerStreaming, marshaller: marshaller), IServerStreamCommandExecutor<TResult>
    where TCommand : class, IServerStreamCommand<TResult>
    where TResult : class
{
    public IAsyncEnumerable<TResult> ExecuteServerStream(IServerStreamCommand<TResult> cmd, CallOptions opts)
        => Execute((TCommand)cmd, opts);

    // the call is disposed when the caller stops enumerating (finished, broke out early or threw), which cancels it on the server if it's still running.
    async IAsyncEnumerable<TResult> Execute(TCommand cmd, CallOptions opts, [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var call = Invoker.AsyncServerStreamingCall(Method, null, opts, cmd);

        while (await call.ResponseStream.MoveNext(ct))
            yield return call.ResponseStream.Current;
    }
}