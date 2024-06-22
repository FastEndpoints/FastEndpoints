using Grpc.AspNetCore.Server.Model;

namespace FastEndpoints;

class ServiceMethodProvider<TExecutor> : IServiceMethodProvider<TExecutor> where TExecutor : class, IMethodBinder<TExecutor>
{
    readonly TExecutor _executor;

    public ServiceMethodProvider(TExecutor executor)
    {
        _executor = executor;
    }

    public void OnServiceMethodDiscovery(ServiceMethodProviderContext<TExecutor> ctx)
        => _executor.Bind(ctx);
}