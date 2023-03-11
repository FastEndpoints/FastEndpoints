using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace FastEndpoints;

internal sealed class ServiceResolver : IServiceResolver
{
    private readonly ConcurrentDictionary<Type, ObjectFactory> factoryCache = new();
    private readonly IServiceProvider rootProvider;
    private readonly IHttpContextAccessor? ctxAccessor;

    public ServiceResolver(IServiceProvider provider, IHttpContextAccessor? ctxAccessor = null)
    {
        rootProvider = provider;
        this.ctxAccessor = ctxAccessor;
    }

    public object CreateInstance(Type type, IServiceProvider? serviceProvider = null)
    {
        var factory = factoryCache.GetOrAdd(type, (t) => ActivatorUtilities.CreateFactory(t, Type.EmptyTypes));
        return factory(serviceProvider ?? ctxAccessor?.HttpContext?.RequestServices ?? rootProvider, null);
    }

    public object CreateSingleton(Type type)
    {
        return ActivatorUtilities.CreateInstance(rootProvider, type);
    }

    public IServiceScope CreateScope() => rootProvider.CreateScope();

    public TService Resolve<TService>() where TService : class
        => ctxAccessor?.HttpContext?.RequestServices.GetRequiredService<TService>() ??
           rootProvider.GetRequiredService<TService>();

    public object Resolve(Type typeOfService)
        => ctxAccessor?.HttpContext?.RequestServices.GetRequiredService(typeOfService) ??
           rootProvider.GetRequiredService(typeOfService);

    public TService? TryResolve<TService>() where TService : class
        => ctxAccessor?.HttpContext?.RequestServices.GetService<TService>() ??
           rootProvider.GetService<TService>();

    public object? TryResolve(Type typeOfService)
        => ctxAccessor?.HttpContext?.RequestServices.GetService(typeOfService) ??
           rootProvider.GetService(typeOfService);
}