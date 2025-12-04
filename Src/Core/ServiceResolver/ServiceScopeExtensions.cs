using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// service scope extensions
/// </summary>
public static class ServiceScopeExtensions
{
    /// <param name="scope"></param>
    extension(IServiceScope scope)
    {
        /// <summary>
        /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
        /// </summary>
        /// <typeparam name="TService">the type of the service to resolve</typeparam>
        public TService? TryResolve<TService>() where TService : class
            => scope.ServiceProvider.GetService<TService>();

        /// <summary>
        /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
        /// </summary>
        /// <param name="typeOfService">the type of the service to resolve</param>
        public object? TryResolve(Type typeOfService)
            => scope.ServiceProvider.GetService(typeOfService);

        /// <summary>
        /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
        /// </summary>
        /// <typeparam name="TService">the type of the service to resolve</typeparam>
        /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
        public TService Resolve<TService>() where TService : class
            => scope.ServiceProvider.GetRequiredService<TService>();

        /// <summary>
        /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
        /// </summary>
        /// <param name="typeOfService">the type of the service to resolve</param>
        /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
        public object Resolve(Type typeOfService)
            => scope.ServiceProvider.GetRequiredService(typeOfService);

        /// <summary>
        /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
        /// </summary>
        /// <param name="keyName">key name</param>
        /// <typeparam name="TService">the type of the service to resolve</typeparam>
        public TService? TryResolve<TService>(string keyName) where TService : class
            => scope.ServiceProvider.GetKeyedService<TService>(keyName);

        /// <summary>
        /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
        /// </summary>
        /// <param name="typeOfService">the type of the service to resolve</param>
        /// <param name="keyName">key name</param>
        public object? TryResolve(Type typeOfService, string keyName)
        {
            if (scope.ServiceProvider is IKeyedServiceProvider sp)
                return sp.GetKeyedService(typeOfService, keyName);

            throw new InvalidOperationException("Keyed services not supported!");
        }

        /// <summary>
        /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
        /// </summary>
        /// <param name="keyName">key name</param>
        /// <typeparam name="TService">the type of the service to resolve</typeparam>
        /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
        public TService Resolve<TService>(string keyName) where TService : class
            => scope.ServiceProvider.GetRequiredKeyedService<TService>(keyName);

        /// <summary>
        /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
        /// </summary>
        /// <param name="typeOfService">the type of the service to resolve</param>
        /// <param name="keyName">key name</param>
        /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
        public object Resolve(Type typeOfService, string keyName)
            => scope.ServiceProvider.GetRequiredKeyedService(typeOfService, keyName);
    }
}