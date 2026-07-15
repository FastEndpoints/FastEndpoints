using Grpc.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        //built lazily on first reflection request, by which time every handler's Bind has populated the registry.
        sc.TryAddSingleton(sp => new ReflectionServiceImpl(Descriptors(sp)));   //grpc.reflection.v1alpha
        sc.TryAddSingleton(sp => new ReflectionV1ServiceImpl(Descriptors(sp))); //grpc.reflection.v1

        return sc;

        static IEnumerable<ServiceDescriptor> Descriptors(IServiceProvider sp)
            => CommandDescriptorFactory.Build(sp.GetRequiredService<RpcSchemaRegistry>(), sp.GetRequiredService<IRpcMarshallerFactory>());
    }

    /// <summary>
    /// maps the gRPC server reflection service for the command handlers this server hosts.
    /// <para>
    /// IMPORTANT: call <see cref="AddHandlerReflection" /> at startup as well.
    /// </para>
    /// </summary>
    /// <param name="b"></param>
    public static IEndpointRouteBuilder MapHandlerReflection(this IEndpointRouteBuilder b)
    {
        //both versions are mapped: modern grpcurl asks for v1 and falls back to v1alpha for older servers.
        b.MapGrpcService<ReflectionServiceImpl>();
        b.MapGrpcService<ReflectionV1ServiceImpl>();

        return b;
    }
}
