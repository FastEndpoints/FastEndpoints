using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable UnusedMemberInSuper.Global

namespace FastEndpoints;

abstract class BaseHandlerExecutor<TCommand, THandler, TResult, TSelf> : IMethodBinder<TSelf>
    where TCommand : class
    where THandler : class
    where TResult : class
    where TSelf : class
{
    protected static readonly ObjectFactory HandlerFactory = ActivatorUtilities.CreateFactory(typeof(THandler), Type.EmptyTypes);

    protected abstract MethodType MethodType();

    protected abstract void AddMethodToCtx(ServiceMethodProviderContext<TSelf> ctx,
                                           Method<TCommand, TResult> method,
                                           List<object> metadata);

    protected virtual Task<TResult> ExecuteUnary(TSelf _, TCommand cmd, ServerCallContext ctx)
        => throw new NotImplementedException();

    protected virtual Task ExecuteServerStream(TSelf _,
                                               TCommand cmd,
                                               IServerStreamWriter<TResult> responseStream,
                                               ServerCallContext ctx)
        => throw new NotImplementedException();

    protected virtual Task<TResult> ExecuteClientStream(TSelf _,
                                                        IAsyncStreamReader<TCommand> requestStream,
                                                        ServerCallContext serverCallContext)
        => throw new NotImplementedException();

    static readonly string[] _httpPost = { "POST" };

    public void Bind(ServiceMethodProviderContext<TSelf> ctx)
    {
        var tExecutor = typeof(TSelf);

        var method = new Method<TCommand, TResult>(
            type: MethodType(),
            serviceName: typeof(TCommand).FullName!,
            name: "",
            requestMarshaller: new MessagePackMarshaller<TCommand>(),
            responseMarshaller: new MessagePackMarshaller<TResult>());

        var metadata = new List<object>();
        var handlerAttributes = HandlerExecMethodAttributes(tExecutor);
        if (handlerAttributes?.Length > 0)
            metadata.AddRange(handlerAttributes);
        metadata.Add(new HttpMethodMetadata(_httpPost, acceptCorsPreflight: true));

        AddMethodToCtx(ctx, method, metadata);
    }

    static object[]? HandlerExecMethodAttributes(Type tExecutor)
    {
        var tHandler = tExecutor.GenericTypeArguments[1];
        var execMethod = tHandler.GetMethod(nameof(ICommandHandler<ICommand<object>, object>.ExecuteAsync));

        return execMethod?.GetCustomAttributes(false);
    }
}