using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace FastEndpoints;

//this class is instantiated by either the IOC container in normal mode
//or by Factory.AddTestServices() method in unit testing mode
#if NET8_0_OR_GREATER
[UnconditionalSuppressMessage("aot", "IL2067"), UnconditionalSuppressMessage("aot", "IL2026"), UnconditionalSuppressMessage("aot", "IL2070")]
#endif
sealed class ServiceResolver(IServiceProvider provider, IHttpContextAccessor? ctxAccessor = null, bool isUnitTestMode = false) : IServiceResolver
{
    static IServiceResolver? _instance;

    /// <summary>
    /// Indicates whether the service resolver is not set.
    /// </summary>
    internal static bool InstanceNotSet => _instance is null;

    /// <summary>
    /// Gets the service resolver.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    internal static IServiceResolver Instance
    {
        get => _instance ?? throw new InvalidOperationException("Service resolver is null! Have you done the unit test setup correctly?");
        set => _instance = value;
    }

    readonly ConcurrentDictionary<Type, ObjectFactory> _factoryCache = new();
    readonly ConcurrentDictionary<Type, object> _singletonCache = new();

    public object CreateInstance(Type type, IServiceProvider? serviceProvider = null)
    {
        var factory = _factoryCache.GetOrAdd(type, ValueFactory);

        return factory(serviceProvider ?? ctxAccessor?.HttpContext?.RequestServices ?? provider, null);

        static ObjectFactory ValueFactory(Type t)
            => ActivatorUtilities.CreateFactory(t, Type.EmptyTypes);
    }

    public object CreateSingleton(Type type)
    {
        return _singletonCache.GetOrAdd(type, ValueFactory, (ctxAccessor, provider));

        static object ValueFactory(Type t, (IHttpContextAccessor? ctxAccessor, IServiceProvider provider) args)
            => ActivatorUtilities.GetServiceOrCreateInstance(args.ctxAccessor?.HttpContext?.RequestServices ?? args.provider, t);
    }

    public IServiceScope CreateScope()
        => isUnitTestMode
               ? ctxAccessor?.HttpContext?.RequestServices.CreateScope() ??
                 throw new InvalidOperationException("Please follow documentation to configure unit test environment properly!")
               : provider.CreateScope();

    public TService Resolve<TService>() where TService : class
        => ctxAccessor?.HttpContext?.RequestServices.GetRequiredService<TService>() ??
           provider.GetRequiredService<TService>();

    public object Resolve(Type typeOfService)
        => ctxAccessor?.HttpContext?.RequestServices.GetRequiredService(typeOfService) ??
           provider.GetRequiredService(typeOfService);

    //when a request scope is active, resolve solely from it instead of falling back to the captured root 'provider'.
    //the request scope is a child of the root, so it already sees every registered service; the '?? provider' fallback
    //would only ever return null for an unregistered service anyway. but 'provider' is a process-wide captured root, and
    //in multi-host setups (e.g. several WebApplicationFactory instances in one test run) the static ServiceResolver.Instance
    //can outlive the host that set it - querying that disposed root throws ObjectDisposedException. only fall back to the
    //root when there is no active request scope (background command/event execution or unit-test setup).

    public TService? TryResolve<TService>() where TService : class
        => ctxAccessor?.HttpContext?.RequestServices is { } rs
               ? rs.GetService<TService>()
               : provider.GetService<TService>();

    public object? TryResolve(Type typeOfService)
        => ctxAccessor?.HttpContext?.RequestServices is { } rs
               ? rs.GetService(typeOfService)
               : provider.GetService(typeOfService);

    public TService? TryResolve<TService>(string keyName) where TService : class
        => ctxAccessor?.HttpContext?.RequestServices is { } rs
               ? rs.GetKeyedService<TService>(keyName)
               : provider.GetKeyedService<TService>(keyName);

    public object? TryResolve(Type typeOfService, string keyName)
    {
        if ((ctxAccessor?.HttpContext?.RequestServices ?? provider) is IKeyedServiceProvider p)
            return p.GetKeyedService(typeOfService, keyName);

        throw new InvalidOperationException("Keyed services not supported!");
    }

    public TService Resolve<TService>(string keyName) where TService : class
        => ctxAccessor?.HttpContext?.RequestServices.GetRequiredKeyedService<TService>(keyName) ??
           provider.GetRequiredKeyedService<TService>(keyName);

    public object Resolve(Type typeOfService, string keyName)
        => ctxAccessor?.HttpContext?.RequestServices.GetRequiredKeyedService(typeOfService, keyName) ??
           provider.GetRequiredKeyedService(typeOfService, keyName);
}