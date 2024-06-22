using Grpc.AspNetCore.Server.Model;
using Grpc.Core;

namespace FastEndpoints;

sealed class UnaryHandlerExecutor<TCommand, THandler, TResult>
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
        => ctx.AddUnaryMethod(method, metadata, ExecuteUnary);

    protected override Task<TResult> ExecuteUnary(UnaryHandlerExecutor<TCommand, THandler, TResult> _, TCommand cmd, ServerCallContext ctx)
    {
        var handler = (THandler)HandlerFactory(ctx.GetHttpContext().RequestServices, null);

        return handler.ExecuteAsync(cmd, ctx.CancellationToken);
    }
}