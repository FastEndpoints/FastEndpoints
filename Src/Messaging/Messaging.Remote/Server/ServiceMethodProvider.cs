using System.Diagnostics.CodeAnalysis;
using Grpc.AspNetCore.Server.Model;

namespace FastEndpoints;

class ServiceMethodProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] TExecutor>(TExecutor executor) : IServiceMethodProvider<TExecutor>
    where TExecutor : class, IMethodBinder<TExecutor>
{
    public void OnServiceMethodDiscovery(ServiceMethodProviderContext<TExecutor> ctx)
        => executor.Bind(ctx);
}