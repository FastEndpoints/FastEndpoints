using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// Defines the basic interface for a pre-processor context, containing essential properties
/// to access request, and associated processing details.
/// </summary>
public interface IPreProcessorContext
{
    /// <summary>
    /// Gets the request object.
    /// </summary>
    object Request { get; }

    /// <summary>
    /// Gets the <see cref="HttpContext"/> associated with the current request.
    /// </summary>
    HttpContext HttpContext { get; }

    /// <summary>
    /// Gets a collection of <see cref="ValidationFailure"/> that occurred during processing.
    /// </summary>
    List<ValidationFailure> ValidationFailures { get; }

    /// <summary>
    /// Determines if any validation failures have occurred during processing.
    /// </summary>
    sealed bool HasValidationFailures => ValidationFailures.Any();
}

/// <summary>
/// Defines the generic interface for a pre-processor context with a specific type for the request.
/// </summary>
/// <typeparam name="TRequest">The type of the request object, which must be non-nullable.</typeparam>
public interface IPreProcessorContext<out TRequest> : IPreProcessorContext
    where TRequest : notnull
{
    /// <summary>
    /// Gets the request object of the generic type <typeparamref name="TRequest"/>.
    /// This hides the non-generic version from <see cref="IPreProcessorContext"/>.
    /// </summary>
    new TRequest Request { get; }

    /// <summary>
    /// Explicit implementation to return the request object from the non-generic context.
    /// </summary>
    object IPreProcessorContext.Request => Request;
}