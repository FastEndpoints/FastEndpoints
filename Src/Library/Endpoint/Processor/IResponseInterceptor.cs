using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// interface for defining a response interceptor to be executed before the main endpoint handler executes
/// </summary>
public interface IResponseInterceptor
{
    /// <summary>
    /// implement this method to intercept the http response with the use of SendInterceptedAsync() method.
    /// </summary>
    /// <param name="response">the response object</param>
    /// <param name="statusCode"></param>
    /// <param name="ctx">the http context of the current request</param>
    /// <param name="failures">the collection of validation failures for the endpoint</param>
    /// <param name="ct">cancellation token</param>
    Task InterceptResponseAsync(object response,
                                int statusCode,
                                HttpContext ctx,
                                IReadOnlyCollection<ValidationFailure> failures,
                                CancellationToken ct);
}