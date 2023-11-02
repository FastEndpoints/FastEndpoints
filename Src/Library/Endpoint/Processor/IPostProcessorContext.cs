using System.Runtime.ExceptionServices;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// Defines the basic interface for a post-processor context, containing essential properties
/// to access request, response, and associated processing details.
/// </summary>
public interface IPostProcessorContext
{
    /// <summary>
    /// Gets the request object.
    /// </summary>
    object Request { get; }

    /// <summary>
    /// Gets the response object, which may be null if the response is not available.
    /// </summary>
    object? Response { get; }

    /// <summary>
    /// Gets the <see cref="HttpContext"/> associated with the current request and response.
    /// </summary>
    HttpContext HttpContext { get; }

    /// <summary>
    /// Gets a read-only collection of <see cref="ValidationFailure"/> that occurred during processing.
    /// </summary>
    IReadOnlyCollection<ValidationFailure> ValidationFailures { get; }

    /// <summary>
    /// Gets information about any exception that was thrown during processing.
    /// This will be null if no exception has occurred.
    /// </summary>
    ExceptionDispatchInfo? ExceptionDispatchInfo { get; }

    /// <summary>
    /// Determines if an exception has occurred during processing.
    /// </summary>
    sealed bool HasExceptionOccurred => ExceptionDispatchInfo is not null;

    /// <summary>
    /// Determines if any validation failures have occurred during processing.
    /// </summary>
    sealed bool HasValidationFailures => ValidationFailures.Any();
}

/// <summary>
/// Defines the generic interface for a post-processor context with specific types for the request and response.
/// </summary>
/// <typeparam name="TRequest">The type of the request object, which must be non-nullable.</typeparam>
/// <typeparam name="TResponse">The type of the response object.</typeparam>
public interface IPostProcessorContext<out TRequest, out TResponse> : IPostProcessorContext
    where TRequest : notnull
{
    /// <summary>
    /// Gets the request object of the generic type <typeparamref name="TRequest"/>.
    /// This hides the non-generic version from <see cref="IPostProcessorContext"/>.
    /// </summary>
    new TRequest Request { get; }

    /// <summary>
    /// Gets the response object of the generic type <typeparamref name="TResponse"/>,
    /// which may be null if the response is not available. This hides the non-generic
    /// version from <see cref="IPostProcessorContext"/>.
    /// </summary>
    new TResponse? Response { get; }

    /// <summary>
    /// Explicit implementation to return the request object from the non-generic context.
    /// </summary>
    object IPostProcessorContext.Request => Request;

    /// <summary>
    /// Explicit implementation to return the response object from the non-generic context.
    /// </summary>
    object? IPostProcessorContext.Response => Response;
}
