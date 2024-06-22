using Grpc.AspNetCore.Server;
using Grpc.AspNetCore.Server.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FastEndpoints;

/// <summary>
/// gRPC handler server extensions
/// </summary>
public static class HandlerServerExtensions
{
    /// <summary>
    /// configure the handler server which will host a collection of command handlers and event hubs. this should only be called once per application.
    /// <para>
    /// IMPORTANT: specify which handlers/hubs this server will be hosting via
    /// <see cref="MapHandlers{TStorageRecord, TStorageProvider}(IEndpointRouteBuilder, Action{HandlerOptions{TStorageRecord, TStorageProvider}})" /> method.
    /// </para>
    /// </summary>
    /// <param name="bld"></param>
    /// <param name="o">optional grpc service settings</param>
    public static IGrpcServerBuilder AddHandlerServer(this WebApplicationBuilder bld, Action<GrpcServiceOptions>? o = null)
        => AddHandlerServer(bld.Services, o);

    /// <summary>
    /// configure the handler server which will host a collection of command handlers. this should only be called once per application.
    /// <para>
    /// IMPORTANT: specify which handlers this server will be hosting via
    /// <see cref="MapHandlers{TStorageRecord, TStorageProvider}(IEndpointRouteBuilder, Action{HandlerOptions{TStorageRecord, TStorageProvider}})" /> method.
    /// </para>
    /// </summary>
    /// <param name="sc"></param>
    /// <param name="o">optional grpc service settings</param>
    public static IGrpcServerBuilder AddHandlerServer(this IServiceCollection sc, Action<GrpcServiceOptions>? o = null)
    {
        sc.TryAddSingleton(typeof(VoidHandlerExecutor<,>));
        sc.TryAddSingleton(typeof(UnaryHandlerExecutor<,,>));
        sc.TryAddSingleton(typeof(ServerStreamHandlerExecutor<,,>));
        sc.TryAddSingleton(typeof(ClientStreamHandlerExecutor<,,>));
        sc.TryAddSingleton(typeof(EventHub<,,>));
        sc.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IServiceMethodProvider<>), typeof(ServiceMethodProvider<>)));

        return sc.AddGrpc(DefaultOpts + o);

        void DefaultOpts(GrpcServiceOptions opts)
            => opts.IgnoreUnknownServices = true;
    }

    /// <summary>
    /// specify which handlers/event hubs this server will be hosting. the in-memory storage provider will be used.
    /// </summary>
    /// <param name="b"></param>
    /// <param name="h">handler options</param>
    public static IEndpointRouteBuilder MapHandlers(this IEndpointRouteBuilder b,
                                                    Action<HandlerOptions<InMemoryEventStorageRecord, InMemoryEventHubStorage>> h)
    {
        h(new(b));

        return b;
    }

    /// <summary>
    /// specify which handlers/event hubs this server will be hosting together with a custom storage provider
    /// </summary>
    /// <typeparam name="TStorageRecord">the type of the event storage record</typeparam>
    /// <typeparam name="TStorageProvider">the type of the event storage provider</typeparam>
    /// <param name="b"></param>
    /// <param name="h">handler options</param>
    public static IEndpointRouteBuilder MapHandlers<TStorageRecord, TStorageProvider>(this IEndpointRouteBuilder b,
                                                                                      Action<HandlerOptions<TStorageRecord, TStorageProvider>> h)
        where TStorageRecord : class, IEventStorageRecord, new()
        where TStorageProvider : class, IEventHubStorageProvider<TStorageRecord>
    {
        h(new(b));

        return b;
    }
}