using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// defines the basic interface for a post-processor context, containing essential properties
/// to access request, response, and associated processing details.
/// </summary>
public interface IPostProcessorContext
{
    /// <summary>
    /// gets the request object.
    /// </summary>
    object Request { get; }

    /// <summary>
    /// gets the response object, which may be null if the response is not available.
    /// </summary>
    object? Response { get; }

    /// <summary>
    /// gets the <see cref="HttpContext" /> associated with the current request and response.
    /// </summary>
    HttpContext HttpContext { get; }

    /// <summary>
    /// gets a read-only collection of <see cref="ValidationFailure" /> that occurred during processing.
    /// </summary>
    IReadOnlyCollection<ValidationFailure> ValidationFailures { get; }

    /// <summary>
    /// gets information about any exception that was thrown during processing.
    /// this will be null if no exception has occurred.
    /// </summary>
    ExceptionDispatchInfo? ExceptionDispatchInfo { get; }

    /// <summary>
    /// determines if an exception has occurred during processing.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ExceptionDispatchInfo))]
    sealed bool HasExceptionOccurred => ExceptionDispatchInfo is not null;

    /// <summary>
    /// determines if any validation failures have occurred during processing.
    /// </summary>
    sealed bool HasValidationFailures => ValidationFailures.Count > 0;

    /// <summary>
    /// call this method if you're handling the captured exception (via <see cref="ExceptionDispatchInfo" />) in a post-processor and the exception should not be thrown.
    /// not calling this method will result in the captured exception being thrown after all the post-processors have run.
    /// </summary>
    public void MarkExceptionAsHandled()
        => HttpContext.MarkEdiHandled();
}

/// <summary>
/// defines the generic interface for a post-processor context with specific types for the request and response.
/// </summary>
/// <typeparam name="TRequest">the type of the request object, which must be non-nullable.</typeparam>
/// <typeparam name="TResponse">the type of the response object.</typeparam>
public interface IPostProcessorContext<out TRequest, out TResponse> : IPostProcessorContext
{
    /// <summary>
    /// gets the request object of the generic type <typeparamref name="TRequest" />.
    /// this hides the non-generic version from <see cref="IPostProcessorContext" />.
    /// </summary>
    [NotNull]
    new TRequest Request { get; }

    /// <summary>
    /// gets the response object of the generic type <typeparamref name="TResponse" />,
    /// which may be null if the response is not available. this hides the non-generic
    /// version from <see cref="IPostProcessorContext" />.
    /// </summary>
    new TResponse? Response { get; }

    /// <summary>
    /// explicit implementation to return the request object from the non-generic context.
    /// </summary>
    object IPostProcessorContext.Request => Request;

    /// <summary>
    /// explicit implementation to return the response object from the non-generic context.
    /// </summary>
    object? IPostProcessorContext.Response => Response;
}