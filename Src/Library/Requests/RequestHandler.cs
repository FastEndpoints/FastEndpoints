using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// inherit this base class to handle requests sent using Request/Response pattern
/// <para>WARNING: request handlers are singletons. DO NOT maintain state in them. Use the <c>Resolve*()</c> methods to obtain dependencies.</para>
/// </summary>
/// <typeparam name="TRequest">the type of the request to handle</typeparam>
/// <typeparam name="TResult">the type of the response result</typeparam>
public abstract class FastRequestHandler<TRequest, TResult> : IRequestHandler<TRequest, TResult>, IServiceResolver where TRequest : IRequest<TResult>
{
    /// <summary>
    /// this method will be called when an request of the specified type is published.
    /// </summary>
    /// <param name="requestModel">the request model/dto received</param>
    /// <param name="ct">an optional cancellation token</param>
    public abstract Task<TResult> HandleAsync(TRequest requestModel, CancellationToken ct);

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    public TService? TryResolve<TService>() where TService : class => IServiceResolver.RootServiceProvider.GetService<TService>();
    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    public object? TryResolve(Type typeOfService) => IServiceResolver.RootServiceProvider.GetService(typeOfService);
    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public TService Resolve<TService>() where TService : class => IServiceResolver.RootServiceProvider.GetRequiredService<TService>();
    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public object Resolve(Type typeOfService) => IServiceResolver.RootServiceProvider.GetRequiredService(typeOfService);
    /// <summary>
    /// if you'd like to resolve scoped or transient services from the DI container, obtain a service scope from this method and dispose the scope when the work is complete.
    ///<para>
    /// <code>
    /// using var scope = CreateScope();
    /// var scopedService = scope.ServiceProvider.GetService(...);
    /// </code>
    /// </para>
    /// </summary>
    public IServiceScope CreateScope() => IServiceResolver.RootServiceProvider.CreateScope();

    #region equality check

    //equality will be checked when discovered concrete handlers are being added to RequestBase.handlerDict HashSet<T>
    //we need this check to be done on the type of the handler instead of the default instance equality check
    //to prrequest duplicate handlers being added to the hash set if/when multiple instances of the app are being run
    //under the same app domain such as OrchardCore multi-tenancy. ex: https://github.com/FastEndpoints/Library/issues/208

    public override bool Equals(object? obj) => obj?.GetType() == GetType();
    public override int GetHashCode() => GetType().GetHashCode();
    #endregion
}