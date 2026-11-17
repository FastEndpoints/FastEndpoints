using System.Diagnostics.CodeAnalysis;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// ReSharper disable UnusedMemberInSuper.Global
// ReSharper disable UseSymbolAlias

namespace FastEndpoints;

abstract class BaseHandlerExecutor<TCommand, THandler, TResult, TSelf> : IMethodBinder<TSelf>
    where TCommand : class
    where THandler : class
    where TResult : class
    where TSelf : class
{
    protected static readonly ObjectFactory HandlerFactory = ActivatorUtilities.CreateFactory(typeof(THandler), Type.EmptyTypes);

    protected abstract MethodType MethodType();

    protected abstract void AddMethodToCtx(ServiceMethodProviderContext<TSelf> ctx, Method<TCommand, TResult> method, List<object> metadata);

    protected static THandler CreateHandler(ServerCallContext ctx)
    {
        var handler = (THandler)HandlerFactory(ctx.GetHttpContext().RequestServices, null);

        // ReSharper disable once SuspiciousTypeConversion.Global
        if (handler is IHasServerCallContext scc)
            scc.ServerCallContext = ctx;

        return handler;
    }

    //caller owns disposal
    protected static CancellationTokenSource LinkWithAppStopping(ServerCallContext ctx)
        => CancellationTokenSource.CreateLinkedTokenSource(
            ctx.CancellationToken,
            ctx.GetHttpContext().RequestServices.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);

    static readonly string[] _httpPost = ["POST"];

    public void Bind(ServiceMethodProviderContext<TSelf> ctx, IRpcMarshallerFactory marshaller, RpcSchemaRegistry schema)
    {
        var tExecutor = typeof(TSelf);
        var serviceName = typeof(TCommand).FullName!;

        var method = new Method<TCommand, TResult>(
            type: MethodType(),
            serviceName: serviceName,
            name: marshaller.MethodName,
            requestMarshaller: marshaller.Create<TCommand>(),
            responseMarshaller: marshaller.Create<TResult>());

        schema.Add(serviceName, MethodType(), typeof(TCommand), typeof(TResult)); //so grpc reflection can describe this handler

        var metadata = new List<object>();
        var handlerAttributes = HandlerExecMethodAttributes(tExecutor);
        if (handlerAttributes?.Length > 0)
            metadata.AddRange(handlerAttributes);
        metadata.Add(new HttpMethodMetadata(_httpPost, acceptCorsPreflight: true));

        AddMethodToCtx(ctx, method, metadata);
    }

    [DynamicDependency(nameof(ICommandHandler<,>.ExecuteAsync), typeof(ICommandHandler<,>))]
    static object[]? HandlerExecMethodAttributes(Type tExecutor)
    {
        var tHandler = tExecutor.GenericTypeArguments[1];
        var execMethod = tHandler.GetMethod(nameof(ICommandHandler<,>.ExecuteAsync));

        return execMethod?.GetCustomAttributes(false);
    }
}