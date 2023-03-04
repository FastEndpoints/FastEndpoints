using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// interface for defining post-processors to be executed after the main endpoint handler is done
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
public interface IPostProcessor<TRequest, TResponse>
{
    /// <summary>
    /// this method is called with the given arguments when the post-processor executes.
    /// </summary>
    /// <param name="req">the request dto object</param>
    /// <param name="res">the response dto object</param>
    /// <param name="ctx">the current http context</param>
    /// <param name="failures">the collection of validation errors of the endpoint</param>
    /// <param name="ct">cancellation token</param>
    Task PostProcessAsync(TRequest req, TResponse res, HttpContext ctx, IReadOnlyCollection<ValidationFailure> failures, CancellationToken ct);
}

/// <summary>
/// interface for defining global post-processors to be executed after the main endpoint handler is done
/// </summary>
public interface IGlobalPostProcessor : IPostProcessor<object, object?> { }