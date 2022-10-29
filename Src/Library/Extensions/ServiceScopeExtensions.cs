using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

public static class ServiceScopeExtensions
{
    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    public static TService? TryResolve<TService>(this IServiceScope scope) where TService : class
        => scope.ServiceProvider.GetService<TService>();

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    public static object? TryResolve(this IServiceScope scope, Type typeOfService)
        => scope.ServiceProvider.GetService(typeOfService);

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public static TService Resolve<TService>(this IServiceScope scope) where TService : class
        => scope.ServiceProvider.GetRequiredService<TService>();

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public static object Resolve(this IServiceScope scope, Type typeOfService)
        => scope.ServiceProvider.GetRequiredService(typeOfService);
}