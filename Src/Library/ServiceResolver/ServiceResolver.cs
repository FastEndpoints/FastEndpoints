using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace FastEndpoints;

internal sealed class ServiceResolver : IServiceResolver
{
    private readonly ConcurrentDictionary<Type, ObjectFactory> factoryCache = new();
    private readonly IServiceProvider rootProvider;
    private readonly IHttpContextAccessor ctxAccessor;

    private readonly bool _testMode;

    public ServiceResolver(IServiceProvider provider, IHttpContextAccessor ctxAccessor, bool isTestMode = false)
    {
        //this class is instantiated by either the IOC container in normal mode
        //or by Factory.AddTestServices() method in unit testing mode

        rootProvider = provider;
        this.ctxAccessor = ctxAccessor;
        _testMode = isTestMode;
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

    public IServiceScope CreateScope()
        => _testMode
            ? ctxAccessor.HttpContext?.RequestServices.CreateScope() ?? throw new InvalidOperationException("Please follow documentation to configure unit test environment properly!")
            : rootProvider.CreateScope();

    public TService Resolve<TService>() where TService : class
        => ctxAccessor.HttpContext?.RequestServices.GetRequiredService<TService>() ??
           rootProvider.GetRequiredService<TService>();

    public object Resolve(Type typeOfService)
        => ctxAccessor.HttpContext?.RequestServices.GetRequiredService(typeOfService) ??
           rootProvider.GetRequiredService(typeOfService);

    public TService? TryResolve<TService>() where TService : class
        => ctxAccessor.HttpContext?.RequestServices.GetService<TService>() ??
           rootProvider.GetService<TService>();

    public object? TryResolve(Type typeOfService)
        => ctxAccessor.HttpContext?.RequestServices.GetService(typeOfService) ??
           rootProvider.GetService(typeOfService);
}