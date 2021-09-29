using FastEndpoints.Validation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints
{
    /// <summary>
    /// use this interface to add functionality to easily map a request dto to a given entity
    /// </summary>
    /// <typeparam name="TEntity">the type of the entity to map to</typeparam>
    public interface IRequest<TEntity>
    {
        /// <summary>
        /// the method that maps a request dto to an entity
        /// </summary>
        TEntity ToEntity();
    }

    /// <summary>
    /// use this interface to populate a response dto from a given entity
    /// </summary>
    /// <typeparam name="TEntity">the type of the entity to populate values from</typeparam>
    public interface IResponse<TEntity>
    {
        /// <summary>
        /// the method that populates response dto from an entity
        /// </summary>
        /// <param name="entity">the type of the entity to populate from</param>
        void FromEntity(TEntity entity);
    }

    /// <summary>
    /// interface for defining pre-processors to be executed before the main endpoint handler is called
    /// </summary>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    public interface IPreProcessor<TRequest>
    {
        Task PreProcessAsync(TRequest req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct);
    }

    /// <summary>
    /// interface for defining post-processors to be executed after the main endpoint handler is done
    /// </summary>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    public interface IPostProcessor<TRequest, TResponse>
    {
        Task PostProcessAsync(TRequest req, TResponse? res, HttpContext ctx, IReadOnlyCollection<ValidationFailure> failures, CancellationToken ct);
    }

    internal interface IEndpoint { }
}
