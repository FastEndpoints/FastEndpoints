using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// keyed service extension methods for <see cref="IServiceScope" /> (.NET 8+)
/// </summary>
public static class ServiceScopeKeyedExtensions
{
    /// <param name="scope">the service scope</param>
    extension(IServiceScope scope)
    {
        /// <summary>
        /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
        /// </summary>
        /// <typeparam name="TService">the type of the service to resolve</typeparam>
        /// <param name="keyName">the key name for resolving keyed service</param>
        public TService? TryResolve<TService>(string keyName) where TService : class
            => scope.ServiceProvider.GetKeyedService<TService>(keyName);

        /// <summary>
        /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
        /// </summary>
        /// <param name="typeOfService">the type of the service to resolve</param>
        /// <param name="keyName">the key name for resolving keyed service</param>
        public object? TryResolve(Type typeOfService, string keyName)
        {
            if (scope.ServiceProvider is IKeyedServiceProvider sp)
                return sp.GetKeyedService(typeOfService, keyName);

            throw new InvalidOperationException("Keyed services not supported!");
        }

        /// <summary>
        /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
        /// </summary>
        /// <typeparam name="TService">the type of the service to resolve</typeparam>
        /// <param name="keyName">the key name for resolving keyed service</param>
        /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
        public TService Resolve<TService>(string keyName) where TService : class
            => scope.ServiceProvider.GetRequiredKeyedService<TService>(keyName);

        /// <summary>
        /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
        /// </summary>
        /// <param name="typeOfService">the type of the service to resolve</param>
        /// <param name="keyName">the key name for resolving keyed service</param>
        /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
        public object Resolve(Type typeOfService, string keyName)
            => scope.ServiceProvider.GetRequiredKeyedService(typeOfService, keyName);
    }
}