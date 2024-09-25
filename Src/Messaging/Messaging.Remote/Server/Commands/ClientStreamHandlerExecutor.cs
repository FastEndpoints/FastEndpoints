using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        var svcProvider = ctx.GetHttpContext().RequestServices;
        var appCancellation = svcProvider.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken, appCancellation);
        var handler = (THandler)HandlerFactory(svcProvider, null);

        // ReSharper disable once SuspiciousTypeConversion.Global
        if (handler is IHasServerCallContext scc)
            scc.ServerCallContext = ctx;

        return handler.ExecuteAsync(requestStream.ReadAllAsync(cts.Token), cts.Token);
    }
}