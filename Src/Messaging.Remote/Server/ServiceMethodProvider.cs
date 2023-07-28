using Grpc.AspNetCore.Server.Model;

namespace FastEndpoints;

internal class ServiceMethodProvider<TExecutor> : IServiceMethodProvider<TExecutor> where TExecutor : class, IMethodBinder<TExecutor>
{
    private readonly TExecutor executor;

    public ServiceMethodProvider(TExecutor executor)
    {
        this.executor = executor;
    }

    public void OnServiceMethodDiscovery(ServiceMethodProviderContext<TExecutor> ctx)
        => executor.Bind(ctx);
}