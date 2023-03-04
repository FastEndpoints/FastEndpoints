using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// interface for defining pre-processors to be executed before the main endpoint handler is called
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
public interface IPreProcessor<TRequest>
{
    /// <summary>
    /// this method is called with the given arguments when the pre-processor executes.
    /// </summary>
    /// <param name="req">the request dto object</param>
    /// <param name="ctx">the current http context</param>
    /// <param name="failures">the collection of validation errors of the endpoint</param>
    /// <param name="ct">cancellation token</param>
    Task PreProcessAsync(TRequest req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct);
}

/// <summary>
/// interface for defining global pre-processors to be executed before the main endpoint handler is called
/// </summary>
public interface IGlobalPreProcessor : IPreProcessor<object> { }