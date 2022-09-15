using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// interface for defining pre-processors to be executed before the main endpoint handler is called
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
public interface IPreProcessor<TRequest>
{
    Task PreProcessAsync(TRequest req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct);
}
