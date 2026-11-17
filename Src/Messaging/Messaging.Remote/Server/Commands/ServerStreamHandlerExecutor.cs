using Grpc.AspNetCore.Server.Model;
using Grpc.Core;

namespace FastEndpoints;

sealed class ServerStreamHandlerExecutor<TCommand, THandler, TResult>(ICommandReceiver<TCommand>? testCommandReceiver = null)
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

    async Task ExecuteServerStream(ServerStreamHandlerExecutor<TCommand, THandler, TResult> _,
                                   TCommand cmd,
                                   IServerStreamWriter<TResult> responseStream,
                                   ServerCallContext ctx)
    {
        testCommandReceiver?.AddCommand(cmd);

        using var cts = LinkWithAppStopping(ctx);
        var handler = CreateHandler(ctx);

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