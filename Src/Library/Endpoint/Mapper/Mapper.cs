using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// use this base class to define domain entity mappers for your endpoints.
/// <para>HINT: entity mappers are used as singletons for performance reasons. do not maintain state in the mappers.</para>
/// </summary>
/// <typeparam name="TRequest">the type of request dto</typeparam>
/// <typeparam name="TResponse">the type of response dto</typeparam>
/// <typeparam name="TEntity">the type of domain entity to map to/from</typeparam>
public abstract class Mapper<TRequest, TResponse, TEntity> : IRequestMapper<TRequest, TEntity>, IResponseMapper<TResponse, TEntity>, IServiceResolverBase
    where TRequest : notnull where TResponse : notnull
{
    public virtual TEntity ToEntity(TRequest r)
        => throw new NotImplementedException($"Please override the {nameof(ToEntity)} method!");

    public virtual Task<TEntity> ToEntityAsync(TRequest r, CancellationToken ct)
        => throw new NotImplementedException($"Please override the {nameof(ToEntityAsync)} method!");

    public virtual TEntity UpdateEntity(TRequest r, TEntity e)
        => throw new NotImplementedException($"Please override the {nameof(UpdateEntity)} method!");

    public virtual Task<TEntity> UpdateEntityAsync(TRequest r, TEntity e, CancellationToken ct)
        => throw new NotImplementedException($"Please override the {nameof(UpdateEntityAsync)} method!");

    public virtual TResponse FromEntity(TEntity e)
        => throw new NotImplementedException($"Please override the {nameof(FromEntity)} method!");

    public virtual Task<TResponse> FromEntityAsync(TEntity e, CancellationToken ct)
        => throw new NotImplementedException($"Please override the {nameof(FromEntityAsync)} method!");

    public TService? TryResolve<TService>() where TService : class
        => ServiceResolver.Instance.TryResolve<TService>();

    public object? TryResolve(Type typeOfService)
        => ServiceResolver.Instance.TryResolve(typeOfService);

    public TService Resolve<TService>() where TService : class
        => ServiceResolver.Instance.Resolve<TService>();

    public object Resolve(Type typeOfService)
        => ServiceResolver.Instance.Resolve(typeOfService);

    public IServiceScope CreateScope()
        => ServiceResolver.Instance.CreateScope();

    public TService? TryResolve<TService>(string keyName) where TService : class
        => ServiceResolver.Instance.TryResolve<TService>(keyName);

    public object? TryResolve(Type typeOfService, string keyName)
        => ServiceResolver.Instance.TryResolve(typeOfService, keyName);

    public TService Resolve<TService>(string keyName) where TService : class
        => ServiceResolver.Instance.Resolve<TService>(keyName);

    public object Resolve(Type typeOfService, string keyName)
        => ServiceResolver.Instance.Resolve(typeOfService, keyName);
}

/// <summary>
/// use this base class to define a domain entity mapper for your endpoints that only has a request dto and no response dto.
/// <para>HINT: entity mappers are used as singletons for performance reasons. do not maintain state in the mappers.</para>
/// </summary>
/// <typeparam name="TRequest">the type of request dto</typeparam>
/// <typeparam name="TEntity">the type of domain entity to map to/from</typeparam>
public abstract class RequestMapper<TRequest, TEntity> : IRequestMapper<TRequest, TEntity>, IServiceResolverBase where TRequest : notnull
{
    public virtual TEntity ToEntity(TRequest r)
        => throw new NotImplementedException($"Please override the {nameof(ToEntity)} method!");

    public virtual Task<TEntity> ToEntityAsync(TRequest r, CancellationToken ct = default)
        => throw new NotImplementedException($"Please override the {nameof(ToEntityAsync)} method!");

    public virtual TEntity UpdateEntity(TRequest r, TEntity e)
        => throw new NotImplementedException($"Please override the {nameof(UpdateEntity)} method!");

    public virtual Task<TEntity> UpdateEntityAsync(TRequest r, TEntity e, CancellationToken ct)
        => throw new NotImplementedException($"Please override the {nameof(UpdateEntityAsync)} method!");

    public TService? TryResolve<TService>() where TService : class
        => ServiceResolver.Instance.TryResolve<TService>();

    public object? TryResolve(Type typeOfService)
        => ServiceResolver.Instance.TryResolve(typeOfService);

    public TService Resolve<TService>() where TService : class
        => ServiceResolver.Instance.Resolve<TService>();

    public object Resolve(Type typeOfService)
        => ServiceResolver.Instance.Resolve(typeOfService);

    public IServiceScope CreateScope()
        => ServiceResolver.Instance.CreateScope();

    public TService? TryResolve<TService>(string keyName) where TService : class
        => ServiceResolver.Instance.TryResolve<TService>(keyName);

    public object? TryResolve(Type typeOfService, string keyName)
        => ServiceResolver.Instance.TryResolve(typeOfService, keyName);

    public TService Resolve<TService>(string keyName) where TService : class
        => ServiceResolver.Instance.Resolve<TService>(keyName);

    public object Resolve(Type typeOfService, string keyName)
        => ServiceResolver.Instance.Resolve(typeOfService, keyName);
}

/// <summary>
/// use this base class to define a domain entity mapper for your endpoints that only has a response dto and no request dto.
/// <para>HINT: entity mappers are used as singletons for performance reasons. do not maintain state in the mappers.</para>
/// </summary>
/// <typeparam name="TResponse">the type of response dto</typeparam>
/// <typeparam name="TEntity">the type of domain entity to map to/from</typeparam>
public abstract class ResponseMapper<TResponse, TEntity> : IResponseMapper<TResponse, TEntity>, IServiceResolverBase where TResponse : notnull
{
    public virtual TResponse FromEntity(TEntity e)
        => throw new NotImplementedException($"Please override the {nameof(FromEntity)} method!");

    public virtual Task<TResponse> FromEntityAsync(TEntity e, CancellationToken ct)
        => throw new NotImplementedException($"Please override the {nameof(FromEntityAsync)} method!");

    public TService? TryResolve<TService>() where TService : class
        => ServiceResolver.Instance.TryResolve<TService>();

    public object? TryResolve(Type typeOfService)
        => ServiceResolver.Instance.TryResolve(typeOfService);

    public TService Resolve<TService>() where TService : class
        => ServiceResolver.Instance.Resolve<TService>();

    public object Resolve(Type typeOfService)
        => ServiceResolver.Instance.Resolve(typeOfService);

    public IServiceScope CreateScope()
        => ServiceResolver.Instance.CreateScope();

    public TService? TryResolve<TService>(string keyName) where TService : class
        => ServiceResolver.Instance.TryResolve<TService>(keyName);

    public object? TryResolve(Type typeOfService, string keyName)
        => ServiceResolver.Instance.TryResolve(typeOfService, keyName);

    public TService Resolve<TService>(string keyName) where TService : class
        => ServiceResolver.Instance.Resolve<TService>(keyName);

    public object Resolve(Type typeOfService, string keyName)
        => ServiceResolver.Instance.Resolve(typeOfService, keyName);
}