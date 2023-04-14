using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.UnitTests;

public class ProxyServiceResolver : IServiceResolver
{
    private readonly IServiceProvider sp;

    public ProxyServiceResolver(IServiceProvider serviceProvider)
    {
        this.sp = serviceProvider;
    }

    public IServiceScope CreateScope() => sp.CreateScope();

    public TService? TryResolve<TService>() where TService : class => sp.GetService<TService>() ?? null;

    public object? TryResolve(Type typeOfService) => sp.GetService(typeOfService) ?? null;

    public TService Resolve<TService>() where TService : class => sp.GetRequiredService<TService>();

    public object Resolve(Type typeOfService) => sp.GetRequiredService(typeOfService);

    public object CreateInstance(Type type, IServiceProvider? serviceProvider = null) => sp.GetRequiredService(type);

    public object CreateSingleton(Type type) => ActivatorUtilities.CreateInstance(sp, type);
}