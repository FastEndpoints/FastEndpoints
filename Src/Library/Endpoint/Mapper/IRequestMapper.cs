// ReSharper disable UnusedParameter.Global

namespace FastEndpoints;

/// <summary>
/// marker interface for request only mappers
/// </summary>
public interface IRequestMapper : IMapper;

/// <summary>
/// use this interface to implement a domain entity mapper for your endpoints that only has a request dto and no response dto.
/// <para>HINT: entity mappers are used as singletons for performance reasons. do not maintain state in the mappers.</para>
/// </summary>
/// <typeparam name="TRequest">the type of request dto</typeparam>
/// <typeparam name="TEntity">the type of domain entity to map to/from</typeparam>
public interface IRequestMapper<in TRequest, TEntity> : IRequestMapper
{
    /// <summary>
    /// implement this method and place the logic for mapping the request dto to the desired domain entity
    /// </summary>
    /// <param name="r">the request dto</param>
    TEntity ToEntity(TRequest r);

    /// <summary>
    /// implement this method and place the logic for mapping the request dto to the desired domain entity
    /// </summary>
    /// <param name="r">the request dto to map from</param>
    /// <param name="ct">a cancellation token</param>
    Task<TEntity> ToEntityAsync(TRequest r, CancellationToken ct);

    /// <summary>
    /// implement this method and place the logic for mapping the updated request dto to the desired domain entity
    /// </summary>
    /// <param name="r">the request dto to update from</param>
    /// <param name="e">the domain entity to update</param>
    TEntity UpdateEntity(TRequest r, TEntity e);

    /// <summary>
    /// implement this method and place the logic for mapping the updated request dto to the desired domain entity
    /// </summary>
    /// <param name="r">the request dto to update from</param>
    /// <param name="e">the domain entity to update</param>
    /// <param name="ct">a cancellation token</param>
    Task<TEntity> UpdateEntityAsync(TRequest r, TEntity e, CancellationToken ct);
}