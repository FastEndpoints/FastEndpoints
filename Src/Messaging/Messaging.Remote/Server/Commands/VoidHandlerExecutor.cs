using Grpc.AspNetCore.Server.Model;
using Grpc.Core;

namespace FastEndpoints;

sealed class VoidHandlerExecutor<TCommand, THandler>
    : BaseHandlerExecutor<TCommand, THandler, EmptyObject, VoidHandlerExecutor<TCommand, THandler>>
    where TCommand : class, ICommand
    where THandler : class, ICommandHandler<TCommand>
{
    protected override MethodType MethodType()
        => Grpc.Core.MethodType.Unary;

    protected override void AddMethodToCtx(ServiceMethodProviderContext<VoidHandlerExecutor<TCommand, THandler>> ctx,
                                           Method<TCommand, EmptyObject> method,
                                           List<object> metadata)
        => ctx.AddUnaryMethod(method, metadata, ExecuteUnary);

    protected override async Task<EmptyObject> ExecuteUnary(VoidHandlerExecutor<TCommand, THandler> _, TCommand cmd, ServerCallContext ctx)
    {
        var handler = (THandler)HandlerFactory(ctx.GetHttpContext().RequestServices, null);
        await handler.ExecuteAsync(cmd, ctx.CancellationToken);

        return EmptyObject.Instance;
    }
}