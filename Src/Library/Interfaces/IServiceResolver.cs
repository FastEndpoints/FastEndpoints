using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace FastEndpoints;

/// <summary>
/// interface used by fastendpoints for resolving services from the DI container.
/// implement this interface and register the implementation in MS DI for customizing service resolving.
/// </summary>
public interface IServiceResolverBase
{
    /// <summary>
    /// if you'd like to resolve scoped or transient services from the MS DI container, obtain a service scope from this method and dispose the scope when the work is complete.
    ///<para>
    /// <code>
    /// using var scope = CreateScope();
    /// var scopedService = scope.ServiceProvider.GetService(...);
    /// </code>
    /// </para>
    /// </summary>
    IServiceScope CreateScope();

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    TService? TryResolve<TService>() where TService : class;

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    object? TryResolve(Type typeOfService);

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    TService Resolve<TService>() where TService : class;

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    object Resolve(Type typeOfService);
}

///<inheritdoc/>
public interface IServiceResolver : IServiceResolverBase
{
    /// <summary>
    /// create an instance of a given type (which may not be registered in the DI container). this method will be called repeatedly per request. so a cached delegate/compiled expression such as <see cref="ActivatorUtilities.CreateFactory(Type, Type[])"/> should be used for instance creation.
    /// </summary>
    /// <param name="type">the type to create an instance of</param>
    /// <param name="serviceProvider">optional service provider</param>
    object CreateInstance(Type type, IServiceProvider? serviceProvider = null);

    /// <summary>
    /// create an instance of a given type (which may not be registered in the DI container) which will be used as a singleton. a utility such as <see cref="ActivatorUtilities.CreateInstance(IServiceProvider, Type, object[])"/> may be used.
    /// </summary>
    /// <param name="type">the type to create an instance of</param>
    object CreateSingleton(Type type);
}

internal class ServiceResolver : IServiceResolver
{
    private readonly ConcurrentDictionary<Type, ObjectFactory> factoryCache = new();
    private readonly IServiceProvider rootProvider;
    private readonly IHttpContextAccessor ctxAccessor;

    public ServiceResolver(IServiceProvider provider, IHttpContextAccessor ctxAccessor)
    {
        rootProvider = provider;
        this.ctxAccessor = ctxAccessor;
    }

    public object CreateInstance(Type type, IServiceProvider? serviceProvider = null)
    {
        var factory = factoryCache.GetOrAdd(type, (t) => ActivatorUtilities.CreateFactory(t, Type.EmptyTypes));
        return factory(serviceProvider ?? ctxAccessor.HttpContext?.RequestServices ?? rootProvider, null);
    }

    public object CreateSingleton(Type type)
    {
        return ActivatorUtilities.CreateInstance(rootProvider, type);
    }

    public IServiceScope CreateScope() => rootProvider.CreateScope();

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