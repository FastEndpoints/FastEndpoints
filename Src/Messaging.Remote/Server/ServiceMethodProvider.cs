using Grpc.AspNetCore.Server.Model;

namespace FastEndpoints;

internal class ServiceMethodProvider<TExecutor> : IServiceMethodProvider<TExecutor> where TExecutor : class, IMethodBinder<TExecutor>, new()
{
    public void OnServiceMethodDiscovery(ServiceMethodProviderContext<TExecutor> ctx)
    {
        var executor = new TExecutor();
        executor.Bind(ctx);
    }
}