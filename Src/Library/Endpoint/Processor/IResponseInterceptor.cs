using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// interface for defining a response interceptor to be executed before the main endpoint handler executes
/// </summary>
/// <param name="TResponse">Intercept responses returning this object type</param>
public interface IResponseInterceptor
{
    Task InterceptResponseAsync(object res, HttpContext ctx, IReadOnlyCollection<ValidationFailure> failures, CancellationToken ct);
}