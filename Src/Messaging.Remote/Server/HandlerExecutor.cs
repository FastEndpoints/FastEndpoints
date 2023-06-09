using Grpc.AspNetCore.Server.Model;
using Grpc.Core;

namespace FastEndpoints;

internal sealed class UnaryHandlerExecutor<TCommand, THandler, TResult>
    : BaseHandlerExecutor<TCommand, THandler, TResult, UnaryHandlerExecutor<TCommand, THandler, TResult>>
        where TCommand : class, ICommand<TResult>
        where THandler : class, ICommandHandler<TCommand, TResult>
        where TResult : class
{
    protected override MethodType MethodType()
        => Grpc.Core.MethodType.Unary;

    protected override void AddMethodToCtx(ServiceMethodProviderContext<UnaryHandlerExecutor<TCommand, THandler, TResult>> ctx,
                                           Method<TCommand, TResult> method,
                                           List<object> metadata)
        => ctx.AddUnaryMethod(method, metadata, Execute);

    protected override Task<TResult> Execute(UnaryHandlerExecutor<TCommand, THandler, TResult> _, TCommand cmd, ServerCallContext ctx)
    {
        var handler = (THandler)_handlerFactory(ctx.GetHttpContext().RequestServices, null);
        return handler.ExecuteAsync(cmd, ctx.CancellationToken);
    }
}

internal sealed class ServerStreamHandlerExecutor<TCommand, THandler, TResult>
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
        => ctx.AddServerStreamingMethod(method, metadata, Execute);

    protected async override Task Execute(ServerStreamHandlerExecutor<TCommand, THandler, TResult> _,
                                          TCommand cmd,
                                          IServerStreamWriter<TResult> responseStream,
                                          ServerCallContext ctx)
    {
        var handler = (THandler)_handlerFactory(ctx.GetHttpContext().RequestServices, null);
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