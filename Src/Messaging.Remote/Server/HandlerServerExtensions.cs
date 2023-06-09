using Grpc.AspNetCore.Server;
using Grpc.AspNetCore.Server.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FastEndpoints;

/// <summary>
/// handler server extensions
/// </summary>
public static class HandlerServerExtensions
{
    /// <summary>
    /// configure the handler server which will host a collection of command handlers. this should only be called once per application.
    /// <para>
    /// IMPORTANT: specify wich handlers this server will be hosting via <see cref="MapHandlers(IEndpointRouteBuilder, Action{HandlerOptions})"/> method.
    /// </para>
    /// </summary>
    /// <param name="o">optional grpc service settings</param>
    public static IGrpcServerBuilder AddHandlerServer(this WebApplicationBuilder bld, Action<GrpcServiceOptions>? o = null)
    {
        bld.Services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IServiceMethodProvider<>), typeof(ServiceMethodProvider<>)));

        Action<GrpcServiceOptions> defaultOpts = o => o.IgnoreUnknownServices = true;

        return bld.Services.AddGrpc(defaultOpts + o);
    }

    /// <summary>
    /// specify wich handlers (<see cref="ICommandHandler{TCommand, TResult}"/>) this server will be hosting
    /// </summary>
    /// <param name="b"></param>
    /// <param name="h"></param>
    public static IEndpointRouteBuilder MapHandlers(this IEndpointRouteBuilder b, Action<HandlerOptions> h)
    {
        h(new HandlerOptions(b));
        return b;
    }
}