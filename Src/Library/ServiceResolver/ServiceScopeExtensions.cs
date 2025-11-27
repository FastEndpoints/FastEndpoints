using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// keyed service extension methods for <see cref="IServiceScope"/> (.NET 8+)
/// </summary>
public static class ServiceScopeKeyedExtensions
{
    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <param name="scope">the service scope</param>
    /// <param name="keyName">the key name for resolving keyed service</param>
    public static TService? TryResolve<TService>(this IServiceScope scope, string keyName) where TService : class
        => scope.ServiceProvider.GetKeyedService<TService>(keyName);

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <param name="scope">the service scope</param>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <param name="keyName">the key name for resolving keyed service</param>
    public static object? TryResolve(this IServiceScope scope, Type typeOfService, string keyName)
    {
        if (scope.ServiceProvider is IKeyedServiceProvider sp)
            return sp.GetKeyedService(typeOfService, keyName);

        throw new InvalidOperationException("Keyed services not supported!");
    }

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <param name="scope">the service scope</param>
    /// <param name="keyName">the key name for resolving keyed service</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public static TService Resolve<TService>(this IServiceScope scope, string keyName) where TService : class
        => scope.ServiceProvider.GetRequiredKeyedService<TService>(keyName);

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <param name="scope">the service scope</param>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <param name="keyName">the key name for resolving keyed service</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public static object Resolve(this IServiceScope scope, Type typeOfService, string keyName)
        => scope.ServiceProvider.GetRequiredKeyedService(typeOfService, keyName);
}