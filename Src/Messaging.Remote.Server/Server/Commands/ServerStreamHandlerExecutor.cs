using Grpc.AspNetCore.Server.Model;
using Grpc.Core;

namespace FastEndpoints;

sealed class ServerStreamHandlerExecutor<TCommand, THandler, TResult>
    : BaseHandlerExecutor<TCommand, THandler, TResult, ServerStreamHandlerExecutor<TCommand, THandler, TResult>>
    where TCommand : class, IServerStreamCommand<TResult>
    where THandler : class, IServerStreamCommandHandler<TCommand, TResult>
    where TResult : class
{
    protected override MethodType MethodType()
        => Grpc.Core.MethodType.ServerStreaming;

    protected override void AddMethodToCtx(ServiceMethodProviderContext<ServerStreamHandlerExecutor<TCommand, THandler, TResult>> ctx,
                                           Method<TCommand, TResult> method,
                                           List<object> metadata)
        => ctx.AddServerStreamingMethod(method, metadata, ExecuteServerStream);

    protected override async Task ExecuteServerStream(ServerStreamHandlerExecutor<TCommand, THandler, TResult> _,
                                                      TCommand cmd,
                                                      IServerStreamWriter<TResult> responseStream,
                                                      ServerCallContext ctx)
    {
        var handler = (THandler)HandlerFactory(ctx.GetHttpContext().RequestServices, null);

        await foreach (var item in handler.ExecuteAsync(cmd, ctx.CancellationToken))
        {
            try
            {
                await responseStream.WriteAsync(item, ctx.CancellationToken);
            }
            catch (OperationCanceledException) { }
        }
    }
}