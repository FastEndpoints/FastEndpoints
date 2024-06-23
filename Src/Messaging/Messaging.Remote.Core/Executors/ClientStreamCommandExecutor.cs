using Grpc.Core;

namespace FastEndpoints;

interface IClientStreamCommandExecutor<in TCommand, TResult> : ICommandExecutor where TCommand : class where TResult : class
{
    Task<TResult> ExecuteClientStream(IAsyncEnumerable<TCommand> commands, CallOptions opts);
}

sealed class ClientStreamCommandExecutor<TCommand, TResult>(ChannelBase channel)
    : BaseCommandExecutor<TCommand, TResult>(channel: channel, methodType: MethodType.ClientStreaming),
      IClientStreamCommandExecutor<TCommand, TResult>
    where TCommand : class
    where TResult : class
{
    public async Task<TResult> ExecuteClientStream(IAsyncEnumerable<TCommand> commands, CallOptions opts)
    {
        var call = Invoker.AsyncClientStreamingCall(Method, null, opts);

        await foreach (var command in commands)
            await call.RequestStream.WriteAsync(command, opts.CancellationToken);

        await call.RequestStream.CompleteAsync();

        return await call.ResponseAsync;
    }
}