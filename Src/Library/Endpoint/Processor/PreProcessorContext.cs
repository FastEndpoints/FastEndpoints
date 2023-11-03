using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// Represents the context for a pre-processing operation with a request.
/// </summary>
/// <typeparam name="TRequest">The type of the request object, which must be non-nullable.</typeparam>
public sealed class PreProcessorContext<TRequest> : IPreProcessorContext<TRequest>
    where TRequest : notnull
{
    /// <summary>
    /// Gets the request associated with the pre-processing context.
    /// This property is required and must be initialized.
    /// </summary>
    public TRequest Request { get; init; }

    /// <summary>
    /// Gets the <see cref="HttpContext"/> associated with the current request.
    /// This property is required and must be initialized.
    /// </summary>
    public HttpContext HttpContext { get; init; }

    /// <summary>
    /// Gets a collection of <see cref="ValidationFailure"/> instances that describe any validation failures.
    /// This property is required and must be initialized.
    /// </summary>
    public List<ValidationFailure> ValidationFailures { get; init; }
}
