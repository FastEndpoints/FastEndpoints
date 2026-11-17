using Grpc.AspNetCore.Server.Model;
using Grpc.Core;

namespace FastEndpoints;

sealed class ClientStreamHandlerExecutor<TCommand, THandler, TResult>
    : BaseHandlerExecutor<TCommand, THandler, TResult, ClientStreamHandlerExecutor<TCommand, THandler, TResult>>
    where TCommand : class
    where THandler : class, IClientStreamCommandHandler<TCommand, TResult>
    where TResult : class
{
    protected override MethodType MethodType()
        => Grpc.Core.MethodType.ClientStreaming;

    protected override void AddMethodToCtx(ServiceMethodProviderContext<ClientStreamHandlerExecutor<TCommand, THandler, TResult>> ctx,
                                           Method<TCommand, TResult> method,
                                           List<object> metadata)
        => ctx.AddClientStreamingMethod(method, metadata, ExecuteClientStream);

    async Task<TResult> ExecuteClientStream(ClientStreamHandlerExecutor<TCommand, THandler, TResult> _,
                                            IAsyncStreamReader<TCommand> requestStream,
                                            ServerCallContext ctx)
    {
        using var cts = LinkWithAppStopping(ctx);

        return await CreateHandler(ctx).ExecuteAsync(requestStream.ReadAllAsync(cts.Token), cts.Token);
    }
}