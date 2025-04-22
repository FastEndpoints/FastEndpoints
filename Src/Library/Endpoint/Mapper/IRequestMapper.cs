namespace FastEndpoints;

/// <summary>
/// marker interface for request only mappers
/// </summary>
public interface IRequestMapper : IMapper;

public interface IRequestMapper<in TRequest, TEntity> : IRequestMapper
{
    /// <summary>
    /// override this method and place the logic for mapping the request dto to the desired domain entity
    /// </summary>
    /// <param name="r">the request dto</param>
    public TEntity ToEntity(TRequest r);

    /// <summary>
    /// override this method and place the logic for mapping the request dto to the desired domain entity
    /// </summary>
    /// <param name="r">the request dto to map from</param>
    /// <param name="ct">a cancellation token</param>
    public Task<TEntity> ToEntityAsync(TRequest r, CancellationToken ct = default);

    /// <summary>
    /// override this method and place the logic for mapping the updated request dto to the desired domain entity
    /// </summary>
    /// <param name="r">the request dto to update from</param>
    /// <param name="e">the domain entity to update</param>
    public TEntity UpdateEntity(TRequest r, TEntity e);

    /// <summary>
    /// override this method and place the logic for mapping the updated request dto to the desired domain entity
    /// </summary>
    /// <param name="r">the request dto to update from</param>
    /// <param name="e">the domain entity to update</param>
    /// <param name="ct">a cancellation token</param>
    public Task<TEntity> UpdateEntityAsync(TRequest r, TEntity e, CancellationToken ct = default);
}