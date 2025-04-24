// ReSharper disable UnusedMemberInSuper.Global
// ReSharper disable UnusedParameter.Global

namespace FastEndpoints;

/// <summary>
/// marker interface for response only mappers
/// </summary>
public interface IResponseMapper : IMapper;

/// <summary>
/// use this interface to implement a domain entity mapper for your endpoints that only has a response dto and no request dto.
/// <para>HINT: entity mappers are used as singletons for performance reasons. do not maintain state in the mappers.</para>
/// </summary>
/// <typeparam name="TResponse">the type of response dto</typeparam>
/// <typeparam name="TEntity">the type of domain entity to map to/from</typeparam>
public interface IResponseMapper<TResponse, in TEntity> : IResponseMapper
{
    /// <summary>
    /// implement this method and place the logic for mapping a domain entity to a response dto
    /// </summary>
    /// <param name="e">the domain entity to map from</param>
    TResponse FromEntity(TEntity e);

    /// <summary>
    /// implement this method and place the logic for mapping a domain entity to a response dto
    /// </summary>
    /// <param name="e">the domain entity to map from</param>
    /// <param name="ct">a cancellation token</param>
    Task<TResponse> FromEntityAsync(TEntity e, CancellationToken ct);
}