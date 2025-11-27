using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// interface used by the messaging library for resolving services from the DI container.
/// extends <see cref="IServiceResolverBase"/> with instance creation capabilities.
/// </summary>
public interface IMessagingServiceResolver : IServiceResolverBase
{
    /// <summary>
    /// create an instance of a given type (which may not be registered in the DI container). this method will be called repeatedly. so a cached
    /// delegate/compiled expression using something like <see cref="ActivatorUtilities.CreateFactory(Type, Type[])" /> should be used for instance creation.
    /// </summary>
    /// <param name="type">the type to create an instance of</param>
    /// <param name="serviceProvider">optional service provider</param>
    object CreateInstance(Type type, IServiceProvider? serviceProvider = null);

    /// <summary>
    /// create an instance of a given type (which may not be registered in the DI container) which will be used as a singleton. a utility such as
    /// <see cref="ActivatorUtilities.CreateInstance(IServiceProvider, Type, object[])" /> may be used. repeated calls with the same input type should return the same
    /// singleton instance by utilizing an internal concurrent/thread-safe cache.
    /// </summary>
    /// <param name="type">the type to create an instance of</param>
    object CreateSingleton(Type type);
}
