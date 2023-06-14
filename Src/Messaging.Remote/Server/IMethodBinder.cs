using Grpc.AspNetCore.Server.Model;

namespace FastEndpoints;

internal interface IMethodBinder<TExecutor> where TExecutor : class
{
    void Bind(ServiceMethodProviderContext<TExecutor> context);
}
