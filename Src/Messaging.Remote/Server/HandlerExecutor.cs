using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace FastEndpoints;

internal interface IHandlerExecutor
{
    void Bind<TExecutor>(ServiceMethodProviderContext<TExecutor> context) where TExecutor : class;
}

internal sealed class HandlerExecutor<TCommand, THandler, TResult> : IHandlerExecutor
    where TCommand : class, ICommand<TResult>
    where THandler : ICommandHandler<TCommand, TResult>
    where TResult : class
{
    private static readonly ObjectFactory handlerFactory = ActivatorUtilities.CreateFactory(typeof(THandler), Type.EmptyTypes);

    private static Task<TResult> Execute(HandlerExecutor<TCommand, THandler, TResult> _, TCommand cmd, ServerCallContext ctx)
    {
        var handler = (THandler)handlerFactory(ctx.GetHttpContext().RequestServices, null);
        return handler.ExecuteAsync(cmd, ctx.CancellationToken);
    }

    public void Bind<TExecutor>(ServiceMethodProviderContext<TExecutor> ctx) where TExecutor : class
    {
        var tExecutor = typeof(TExecutor);

        var executeMethod = tExecutor.GetMethod(nameof(Execute), BindingFlags.NonPublic | BindingFlags.Static)!;

        var invoker = (UnaryServerMethod<TExecutor, TCommand, TResult>)Delegate.CreateDelegate(
            typeof(UnaryServerMethod<TExecutor, TCommand, TResult>),
            executeMethod);

        var method = new Method<TCommand, TResult>(
            type: MethodType.Unary,
            serviceName: typeof(TCommand).FullName!,
            name: "",
            requestMarshaller: new MessagePackMarshaller<TCommand>(),
            responseMarshaller: new MessagePackMarshaller<TResult>());

        var metadata = new List<object>
        {
            // Accepting CORS preflight means gRPC will allow requests with OPTIONS + preflight headers.
            // If CORS middleware hasn't been configured then the request will reach gRPC handler.
            // gRPC will return 405 response and log that CORS has not been configured.
            new HttpMethodMetadata(new[] { "POST" }, acceptCorsPreflight: true)
        };

        ctx.AddUnaryMethod(method, metadata, invoker);
    }
}
