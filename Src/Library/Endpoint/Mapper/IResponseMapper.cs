namespace FastEndpoints;

/// <summary>
/// marker interface for response only mappers
/// </summary>
public interface IResponseMapper : IMapper;

public interface IResponseMapper<TResponse, in TEntity> : IResponseMapper
{
    /// <summary>
    /// override this method and place the logic for mapping a domain entity to a response dto
    /// </summary>
    /// <param name="e">the domain entity to map from</param>
    public TResponse FromEntity(TEntity e);

    /// <summary>
    /// override this method and place the logic for mapping a domain entity to a response dto
    /// </summary>
    /// <param name="e">the domain entity to map from</param>
    /// <param name="ct">a cancellation token</param>
    public Task<TResponse> FromEntityAsync(TEntity e, CancellationToken ct = default);
}