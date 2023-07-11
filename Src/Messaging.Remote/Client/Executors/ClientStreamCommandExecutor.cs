using Grpc.Core;
using Grpc.Net.Client;

namespace FastEndpoints;

internal interface IClientStreamCommandExecutor<TCommand, TResult> : ICommandExecutor where TCommand : class where TResult : class
{
    Task<TResult> ExecuteClientStream(IAsyncEnumerable<TCommand> commands, CallOptions opts);
}

internal sealed class ClientStreamCommandExecutor<TCommand, TResult> : BaseCommandExecutor<TCommand, TResult>, IClientStreamCommandExecutor<TCommand, TResult>
    where TCommand : class
    where TResult : class
{
    public ClientStreamCommandExecutor(GrpcChannel channel)
        : base(channel: channel,
               methodType: MethodType.ClientStreaming)
    { }

    public async Task<TResult> ExecuteClientStream(IAsyncEnumerable<TCommand> commands, CallOptions opts)
    {
        var call = _invoker.AsyncClientStreamingCall(_method, null, opts);

        await foreach (var command in commands)
            await call.RequestStream.WriteAsync(command, opts.CancellationToken);

        await call.RequestStream.CompleteAsync();
        return await call.ResponseAsync;
    }
}