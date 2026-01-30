using System.Diagnostics.CodeAnalysis;
using Grpc.AspNetCore.Server.Model;

namespace FastEndpoints;

interface IMethodBinder<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] TExecutor> where TExecutor : class
{
    void Bind(ServiceMethodProviderContext<TExecutor> context);
}
