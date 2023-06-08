using Grpc.AspNetCore.Server;
using Grpc.AspNetCore.Server.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
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
    /// IMPORTANT: specify wich handlers this server will be hosting via <see cref="MapHandlers(IEndpointRouteBuilder, Action{HandlerRegistry})"/> method.
    /// </para>
    /// </summary>
    /// <param name="o">optional grpc service settings</param>
    public static IGrpcServerBuilder AddHandlerServer(this WebApplicationBuilder bld, Action<GrpcServiceOptions>? o = null)
    {
        bld.WebHost.ConfigureKestrel(o => o.ConfigureEndpointDefaults(o => o.Protocols = HttpProtocols.Http2));
        bld.Services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IServiceMethodProvider<>), typeof(ServiceMethodProvider<>)));
        if (o is null) return bld.Services.AddGrpc();
        return bld.Services.AddGrpc(o);
    }

    /// <summary>
    /// specify wich handlers (<see cref="ICommandHandler{TCommand, TResult}"/>) this server will be hosting
    /// </summary>
    /// <param name="b"></param>
    /// <param name="h"></param>
    public static IEndpointRouteBuilder MapHandlers(this IEndpointRouteBuilder b, Action<HandlerRegistry> h)
    {
        h(new HandlerRegistry(b));
        return b;
    }
}