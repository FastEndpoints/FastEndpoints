using Grpc.Core;

namespace FastEndpoints;

interface IClientStreamCommandExecutor<in TCommand, TResult> : ICommandExecutor where TCommand : class where TResult : class
{
    Task<TResult> ExecuteClientStream(IAsyncEnumerable<TCommand> commands, CallOptions opts);
}

sealed class ClientStreamCommandExecutor<TCommand, TResult>(ChannelBase channel, IRpcMarshallerFactory marshaller)
    : BaseCommandExecutor<TCommand, TResult>(channel: channel, methodType: MethodType.ClientStreaming, marshaller: marshaller), IClientStreamCommandExecutor<TCommand, TResult>
    where TCommand : class
    where TResult : class
{
    public async Task<TResult> ExecuteClientStream(IAsyncEnumerable<TCommand> commands, CallOptions opts)
    {
        // disposing a call that hasn't completed (the command stream or a write failed) cancels it on the server.
        using var call = Invoker.AsyncClientStreamingCall(Method, null, opts);

        await foreach (var command in commands.WithCancellation(opts.CancellationToken))
            await call.RequestStream.WriteAsync(command, opts.CancellationToken);

        await call.RequestStream.CompleteAsync();

        return await call.ResponseAsync;
    }
}