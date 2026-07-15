using FastEndpoints.Messaging.Remote;
using Grpc.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceDescriptor = Google.Protobuf.Reflection.ServiceDescriptor;

namespace FastEndpoints;

/// <summary>
/// gRPC server reflection for a handler server
/// </summary>
public static class HandlerReflectionExtensions
{
    /// <summary>
    /// enables standard gRPC server reflection for the command handlers this server hosts, so that tooling such as grpcurl
    /// and postman can discover and describe them without a hand-authored <c>.proto</c> file.
    /// <para>
    /// IMPORTANT: reflection describes a protobuf schema, so the server must be using the protobuf wire format. supply a
    /// <see cref="ProtobufMarshallerFactory" /> via <c>AddHandlerServer(marshaller: ...)</c>. also map the reflection service
    /// via <see cref="MapHandlerReflection" />.
    /// </para>
    /// </summary>
    /// <param name="sc"></param>
    public static IServiceCollection AddHandlerReflection(this IServiceCollection sc)
    {
        //built once, lazily, on first use - by which time every handler's Bind has populated the registry.
        sc.TryAddSingleton<RpcDescriptors>();
        sc.TryAddSingleton(sp => new ReflectionServiceImpl(sp.GetRequiredService<RpcDescriptors>().Services));   //grpc.reflection.v1alpha
        sc.TryAddSingleton(sp => new ReflectionV1ServiceImpl(sp.GetRequiredService<RpcDescriptors>().Services)); //grpc.reflection.v1

        return sc;
    }

    /// <summary>
    /// maps the gRPC server reflection service for the command handlers this server hosts.
    /// <para>
    /// IMPORTANT: call <see cref="AddHandlerReflection" /> at startup as well.
    /// </para>
    /// <para>
    /// NOTE: as with the stock <c>MapGrpcReflectionService()</c>, the reflection endpoints are anonymous. handlers keep their own
    /// auth, so a caller still can't execute anything, but the published schema is readable by anyone who can reach the port.
    /// chain <c>.RequireAuthorization()</c> on the returned builder to restrict it.
    /// </para>
    /// </summary>
    /// <param name="b"></param>
    public static IEndpointConventionBuilder MapHandlerReflection(this IEndpointRouteBuilder b)
    {
        //surface an unusable configuration (e.g. reflection without the protobuf marshaller) while the app is starting,
        //instead of on some user's first grpcurl call.
        b.ServiceProvider.GetRequiredService<IHostApplicationLifetime>()
         .ApplicationStarted.Register(() => b.ServiceProvider.GetRequiredService<RpcDescriptors>());

        //both versions are mapped: modern grpcurl asks for v1 and falls back to v1alpha for older servers.
        return new CompositeEndpointConventionBuilder(
        [
            b.MapGrpcService<ReflectionServiceImpl>(),
            b.MapGrpcService<ReflectionV1ServiceImpl>()
        ]);
    }
}

//holds the generated descriptors so both reflection services share one build instead of doing the work twice.
sealed class RpcDescriptors(RpcSchemaRegistry registry, IRpcMarshallerFactory marshaller, ILogger<RpcDescriptors> logger)
{
    internal IReadOnlyList<ServiceDescriptor> Services { get; } = CommandDescriptorFactory.Build(registry, marshaller, logger);
}

sealed class CompositeEndpointConventionBuilder(IReadOnlyList<IEndpointConventionBuilder> builders) : IEndpointConventionBuilder
{
    public void Add(Action<EndpointBuilder> convention)
    {
        foreach (var b in builders)
            b.Add(convention);
    }

    public void Finally(Action<EndpointBuilder> finalConvention)
    {
        foreach (var b in builders)
            b.Finally(finalConvention);
    }
}
