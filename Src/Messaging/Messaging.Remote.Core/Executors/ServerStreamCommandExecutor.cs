using Grpc.Core;
using Grpc.Net.Client;

namespace FastEndpoints;

interface IServerStreamCommandExecutor<TResult> : ICommandExecutor where TResult : class
{
    IAsyncStreamReader<TResult> ExecuteServerStream(IServerStreamCommand<TResult> command, CallOptions opts);
}

sealed class ServerStreamCommandExecutor<TCommand, TResult>(GrpcChannel channel)
    : BaseCommandExecutor<TCommand, TResult>(channel: channel, methodType: MethodType.ServerStreaming),
      IServerStreamCommandExecutor<TResult>
    where TCommand : class, IServerStreamCommand<TResult>
    where TResult : class
{
    public IAsyncStreamReader<TResult> ExecuteServerStream(IServerStreamCommand<TResult> cmd, CallOptions opts)
        => Invoker.AsyncServerStreamingCall(Method, null, opts, (TCommand)cmd).ResponseStream;
}