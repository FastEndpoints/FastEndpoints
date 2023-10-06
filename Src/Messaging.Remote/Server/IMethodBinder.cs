using Grpc.AspNetCore.Server.Model;

namespace FastEndpoints;

interface IMethodBinder<TExecutor> where TExecutor : class
{
    void Bind(ServiceMethodProviderContext<TExecutor> context);
}
