using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace FastEndpoints;

/// <summary>
/// default implementation of <see cref="IMessagingServiceResolver"/> using Microsoft.Extensions.DependencyInjection
/// </summary>
public sealed class MessagingServiceResolver : IMessagingServiceResolver
{
    readonly IServiceProvider _provider;
    readonly ConcurrentDictionary<Type, ObjectFactory> _factoryCache = new();
    readonly ConcurrentDictionary<Type, object> _singletonCache = new();

    /// <summary>
    /// creates a new instance of the messaging service resolver
    /// </summary>
    /// <param name="provider">the service provider to use for resolving services</param>
    public MessagingServiceResolver(IServiceProvider provider)
    {
        _provider = provider;
    }

    /// <inheritdoc />
    public IServiceScope CreateScope()
        => _provider.CreateScope();

    /// <inheritdoc />
    public object CreateInstance(Type type, IServiceProvider? serviceProvider = null)
    {
        var factory = _factoryCache.GetOrAdd(type, FactoryInitializer);

        return factory(serviceProvider ?? _provider, null);

        static ObjectFactory FactoryInitializer(Type t)
            => ActivatorUtilities.CreateFactory(t, Type.EmptyTypes);
    }

    /// <inheritdoc />
    public object CreateSingleton(Type type)
        => _singletonCache.GetOrAdd(type, ActivatorUtilities.GetServiceOrCreateInstance(_provider, type));

    /// <inheritdoc />
    public TService Resolve<TService>() where TService : class
        => _provider.GetRequiredService<TService>();

    /// <inheritdoc />
    public object Resolve(Type typeOfService)
        => _provider.GetRequiredService(typeOfService);

    /// <inheritdoc />
    public TService? TryResolve<TService>() where TService : class
        => _provider.GetService<TService>();

    /// <inheritdoc />
    public object? TryResolve(Type typeOfService)
        => _provider.GetService(typeOfService);
}
