namespace FastEndpoints;

/// <summary>
/// interface used by fastendpoints for resolving services from the DI container.
/// implement this interface and register the implementation in MS DI for customizing service resolving.
/// </summary>
public interface IServiceResolver : IServiceResolverBase
{
    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <param name="keyName">the key name for resolving keyed service</param>
    TService? TryResolve<TService>(string keyName) where TService : class;

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <param name="keyName">the key name for resolving keyed service</param>
    object? TryResolve(Type typeOfService, string keyName);

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <param name="keyName">the key name for resolving keyed service</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    TService Resolve<TService>(string keyName) where TService : class;

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <param name="keyName">the key name for resolving keyed service</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    object Resolve(Type typeOfService, string keyName);
}