using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace FastEndpoints;

sealed class ServiceResolver(IServiceProvider provider,
                             IHttpContextAccessor ctxAccessor,
                             bool isUnitTestMode = false) : IServiceResolver
{
    readonly ConcurrentDictionary<Type, ObjectFactory> _factoryCache = new();
    readonly ConcurrentDictionary<Type, object> _singletonCache = new();

    //this class is instantiated by either the IOC container in normal mode
    //or by Factory.AddTestServices() method in unit testing mode

    public object CreateInstance(Type type, IServiceProvider? serviceProvider = null)
    {
        //WARNING: DO NOT DO THIS!!! it results in a perf degradation. no idea why.
        //  factory = _factoryCache.GetOrAdd(type, ActivatorUtilities.CreateFactory(type, Type.EmptyTypes));

        var factory = _factoryCache.GetOrAdd(type, FactoryInitializer);

        return factory(serviceProvider ?? ctxAccessor?.HttpContext?.RequestServices ?? provider, null);

        static ObjectFactory FactoryInitializer(Type t)
            => ActivatorUtilities.CreateFactory(t, Type.EmptyTypes);
    }

    public object CreateSingleton(Type type)
        => _singletonCache.GetOrAdd(type, ActivatorUtilities.GetServiceOrCreateInstance(provider, type));

    public IServiceScope CreateScope()
        => isUnitTestMode
               ? ctxAccessor.HttpContext?.RequestServices.CreateScope() ??
                 throw new InvalidOperationException("Please follow documentation to configure unit test environment properly!")
               : provider.CreateScope();

    public TService Resolve<TService>() where TService : class
        => ctxAccessor.HttpContext?.RequestServices.GetRequiredService<TService>() ??
           provider.GetRequiredService<TService>();

    public object Resolve(Type typeOfService)
        => ctxAccessor.HttpContext?.RequestServices.GetRequiredService(typeOfService) ??
           provider.GetRequiredService(typeOfService);

    public TService? TryResolve<TService>() where TService : class
        => ctxAccessor.HttpContext?.RequestServices.GetService<TService>() ??
           provider.GetService<TService>();

    public object? TryResolve(Type typeOfService)
        => ctxAccessor.HttpContext?.RequestServices.GetService(typeOfService) ??
           provider.GetService(typeOfService);
}