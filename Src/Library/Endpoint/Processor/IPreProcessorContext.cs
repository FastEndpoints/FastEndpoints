using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// defines the basic interface for a pre-processor context, containing essential properties to access request, and associated processing details.
/// </summary>
public interface IPreProcessorContext
{
    /// <summary>
    /// gets the request object. may be null if request binding has failed.
    /// </summary>
    object? Request { get; }

    /// <summary>
    /// gets the <see cref="HttpContext" /> associated with the current request.
    /// </summary>
    HttpContext HttpContext { get; }

    /// <summary>
    /// gets a collection of <see cref="ValidationFailure" /> that occurred during processing.
    /// </summary>
    List<ValidationFailure> ValidationFailures { get; }

    /// <summary>
    /// determines if any validation failures have occurred during processing.
    /// </summary>
    sealed bool HasValidationFailures => ValidationFailures.Count > 0;
}

/// <summary>
/// defines the generic interface for a pre-processor context with a specific type for the request.
/// </summary>
/// <typeparam name="TRequest">The type of the request object, which must be non-nullable.</typeparam>
public interface IPreProcessorContext<out TRequest> : IPreProcessorContext
{
    /// <summary>
    /// gets the request object of the generic type <typeparamref name="TRequest" />.
    /// may be null if request binding has failed.
    /// </summary>
    new TRequest? Request { get; }

    /// <summary>
    /// explicit implementation to return the request object from the non-generic context.
    /// </summary>
    object? IPreProcessorContext.Request => Request;
}