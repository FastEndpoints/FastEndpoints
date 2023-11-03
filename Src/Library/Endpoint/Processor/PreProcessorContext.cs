using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// represents the context for a pre-processing operation with a request.
/// </summary>
/// <typeparam name="TRequest">the type of the request object, which must be non-nullable.</typeparam>
public sealed class PreProcessorContext<TRequest> : IPreProcessorContext<TRequest> where TRequest : notnull
{
    /// <summary>
    /// gets the request associated with the pre-processing context.
    /// </summary>
    public TRequest Request { get; init; }

    /// <summary>
    /// gets the <see cref="HttpContext" /> associated with the current request.
    /// </summary>
    public HttpContext HttpContext { get; init; }

    /// <summary>
    /// gets a collection of <see cref="ValidationFailure" /> instances that describe any validation failures.
    /// </summary>
    public List<ValidationFailure> ValidationFailures { get; init; }

    internal PreProcessorContext(TRequest request, HttpContext httpContext, List<ValidationFailure> validationFailures)
    {
        Request = request;
        HttpContext = httpContext;
        ValidationFailures = validationFailures;
    }
}