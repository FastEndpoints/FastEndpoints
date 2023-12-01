using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace FastEndpoints;

sealed class ServiceResolver : IServiceResolver
{
    readonly ConcurrentDictionary<Type, ObjectFactory> _factoryCache = new();
    readonly ConcurrentDictionary<Type, object> _singletonCache = new();
    readonly IServiceProvider _rootServiceProvider;
    readonly IHttpContextAccessor _ctxAccessor;

    readonly bool _isUnitTestMode;

    public ServiceResolver(IServiceProvider provider, IHttpContextAccessor ctxAccessor, bool isUnitTestMode = false)
    {
        //this class is instantiated by either the IOC container in normal mode
        //or by Factory.AddTestServices() method in unit testing mode

        _rootServiceProvider = provider;
        _ctxAccessor = ctxAccessor;
        _isUnitTestMode = isUnitTestMode;
    }

    public object CreateInstance(Type type, IServiceProvider? serviceProvider = null)
    {
        //WARNING: DO NOT DO THIS!!! it results in a perf degradation. no idea why.
        //  factory = _factoryCache.GetOrAdd(type, ActivatorUtilities.CreateFactory(type, Type.EmptyTypes));
        var factory = _factoryCache.GetOrAdd(type, FactoryInitializer);

        return factory(serviceProvider ?? _ctxAccessor?.HttpContext?.RequestServices ?? _rootServiceProvider, null);

        static ObjectFactory FactoryInitializer(Type t)
            => ActivatorUtilities.CreateFactory(t, Type.EmptyTypes);
    }

    public object CreateSingleton(Type type)
        => _singletonCache.GetOrAdd(type, ActivatorUtilities.GetServiceOrCreateInstance(_rootServiceProvider, type));

    public IServiceScope CreateScope()
        => _isUnitTestMode
               ? _ctxAccessor.HttpContext?.RequestServices.CreateScope() ??
                 throw new InvalidOperationException("Please follow documentation to configure unit test environment properly!")
               : _rootServiceProvider.CreateScope();

    public TService Resolve<TService>() where TService : class
        => _ctxAccessor.HttpContext?.RequestServices.GetRequiredService<TService>() ??
           _rootServiceProvider.GetRequiredService<TService>();

    public object Resolve(Type typeOfService)
        => _ctxAccessor.HttpContext?.RequestServices.GetRequiredService(typeOfService) ??
           _rootServiceProvider.GetRequiredService(typeOfService);

    public TService? TryResolve<TService>() where TService : class
        => _ctxAccessor.HttpContext?.RequestServices.GetService<TService>() ??
           _rootServiceProvider.GetService<TService>();

    public object? TryResolve(Type typeOfService)
        => _ctxAccessor.HttpContext?.RequestServices.GetService(typeOfService) ??
           _rootServiceProvider.GetService(typeOfService);
}