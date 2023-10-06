using Grpc.AspNetCore.Server.Model;

namespace FastEndpoints;

class ServiceMethodProvider<TExecutor> : IServiceMethodProvider<TExecutor> where TExecutor : class, IMethodBinder<TExecutor>
{
    readonly TExecutor executor;

    public ServiceMethodProvider(TExecutor executor)
    {
        this.executor = executor;
    }

    public void OnServiceMethodDiscovery(ServiceMethodProviderContext<TExecutor> ctx)
        => executor.Bind(ctx);
}