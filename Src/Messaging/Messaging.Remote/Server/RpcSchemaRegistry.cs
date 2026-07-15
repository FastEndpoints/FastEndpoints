using System.Collections.Concurrent;
using Grpc.Core;

namespace FastEndpoints;

//records the command services bound to this handler server, so grpc reflection can generate descriptors for them.
//populated by BaseHandlerExecutor.Bind as each handler's method is discovered. event hubs are not registered
//(their "sub" method takes a bare string, which has no protobuf message equivalent).
sealed class RpcSchemaRegistry
{
    readonly ConcurrentDictionary<string, RpcServiceSchema> _services = new();

    internal void Add(string serviceName, MethodType methodType, Type tCommand, Type tResult)
        => _services[serviceName] = new(serviceName, methodType, tCommand, tResult);

    internal IReadOnlyCollection<RpcServiceSchema> Services
        => _services.Values.ToArray();
}

sealed record RpcServiceSchema(string ServiceName, MethodType MethodType, Type CommandType, Type ResultType);
