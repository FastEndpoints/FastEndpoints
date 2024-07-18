using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        var svcProvider = ctx.GetHttpContext().RequestServices;
        var appCancellation = svcProvider.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken, appCancellation);
        var handler = (THandler)HandlerFactory(svcProvider, null);

        await foreach (var item in handler.ExecuteAsync(cmd, cts.Token))
        {
            try
            {
                await responseStream.WriteAsync(item, cts.Token);
            }
            catch (OperationCanceledException) { }
        }
    }
}