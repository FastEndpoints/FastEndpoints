using System.Runtime.ExceptionServices;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// Represents the context for a post-processor that is executed after the main endpoint handler has completed processing.
/// It contains the request, response, associated HTTP context, any validation failures, and exception information if applicable.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public sealed class PostProcessorContext<TRequest, TResponse>
{
    /// <summary>
    /// Gets the request that was received by the endpoint.
    /// </summary>
    public TRequest Request { get; init; }

    /// <summary>
    /// Gets the response produced by the endpoint.
    /// </summary>
    public TResponse Response { get; init; }

    /// <summary>
    /// Gets the HttpContext associated with the current request.
    /// </summary>
    public HttpContext HttpContext { get; init; }

    /// <summary>
    /// Gets a collection of <see cref="ValidationFailure"/> objects representing the validation failures that occurred during processing of the request.
    /// </summary>
    public IReadOnlyCollection<ValidationFailure> ValidationFailures { get; init; }

    /// <summary>
    /// Gets the ExceptionDispatchInfo if an exception was captured during the processing of the request.
    /// </summary>
    /// <remarks>
    /// This property may be null, indicating that no exception was captured.
    /// </remarks>
    public ExceptionDispatchInfo? ExceptionDispatchInfo { get; init; }

    /// <summary>
    /// Gets a value indicating whether an exception was captured during the processing of the request.
    /// </summary>
    /// <value>
    /// True if an exception was captured; otherwise, false.
    /// </value>
    public bool HasExceptionOccurred => ExceptionDispatchInfo is not null;

    /// <summary>
    /// Gets a value indicating whether any validation failures occurred during the processing of the request.
    /// </summary>
    /// <value>
    /// True if there are validation failures; otherwise, false.
    /// </value>
    public bool HasValidationFailures => ValidationFailures.Any();
}
