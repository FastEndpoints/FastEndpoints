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

    protected override Task<TResult> ExecuteClientStream(ClientStreamHandlerExecutor<TCommand, THandler, TResult> _,
                                                         IAsyncStreamReader<TCommand> requestStream,
                                                         ServerCallContext ctx)
    {
        var handler = (THandler)HandlerFactory(ctx.GetHttpContext().RequestServices, null);

        return handler.ExecuteAsync(requestStream.ReadAllAsync(ctx.CancellationToken), ctx.CancellationToken);
    }
}