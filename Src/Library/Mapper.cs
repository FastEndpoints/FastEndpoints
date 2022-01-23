using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// use this base class to define domain entity mappers for your endpoints.
/// <para>HINT: entity mappers are used as singletons for performance reasons. do not maintain state in the mappers.</para>
/// </summary>
/// <typeparam name="TRequest">the type of request dto</typeparam>
/// <typeparam name="TResponse">the type of response dto</typeparam>
/// <typeparam name="TEntity">the type of domain entity to map to/from</typeparam>
public abstract class Mapper<TRequest, TResponse, TEntity> : IEntityMapper, IServiceResolver where TRequest : notnull, new() where TResponse : notnull, new()
{
    /// <summary>
    /// override this method and place the logic for mapping the request dto to the desired domain entity
    /// </summary>
    /// <param name="r">the request dto</param>
    public virtual TEntity ToEntity(TRequest r) => throw new NotImplementedException($"Please override the {nameof(ToEntity)} method!");
    /// <summary>
    /// override this method and place the logic for mapping the request dto to the desired domain entity
    /// </summary>
    /// <param name="r">the request dto to map from</param>
    public virtual Task<TEntity> ToEntityAsync(TRequest r) => throw new NotImplementedException($"Please override the {nameof(ToEntityAsync)} method!");

    /// <summary>
    /// override this method and place the logic for mapping a domain entity to a response dto
    /// </summary>
    /// <param name="e">the domain entity to map from</param>
    public virtual TResponse FromEntity(TEntity e) => throw new NotImplementedException($"Please override the {nameof(FromEntity)} method!");
    /// <summary>
    /// override this method and place the logic for mapping a domain entity to a response dto
    /// </summary>
    /// <param name="e">the domain entity to map from</param>
    public virtual Task<TResponse> FromEntityAsync(TEntity e) => throw new NotImplementedException($"Please override the {nameof(FromEntityAsync)} method!");

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    public TService? TryResolve<TService>() where TService : class
        => IServiceResolver.HttpContextAccessor.HttpContext?.RequestServices.GetService<TService>();

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    public object? TryResolve(Type typeOfService)
        => IServiceResolver.HttpContextAccessor.HttpContext?.RequestServices.GetService(typeOfService);

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public TService Resolve<TService>() where TService : class
        => IServiceResolver.HttpContextAccessor.HttpContext!.RequestServices.GetRequiredService<TService>();

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public object Resolve(Type typeOfService)
        => IServiceResolver.HttpContextAccessor.HttpContext!.RequestServices.GetRequiredService(typeOfService);
}