using System.Runtime.ExceptionServices;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// Represents the context for a post-processing operation with a request and response pair.
/// </summary>
/// <typeparam name="TRequest">The type of the request object, which must be non-nullable.</typeparam>
/// <typeparam name="TResponse">The type of the response object.</typeparam>
public sealed class PostProcessorContext<TRequest, TResponse> : IPostProcessorContext<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Gets the request associated with the post-processing context.
    /// This property is required and must be initialized.
    /// </summary>
    public TRequest Request { get; init; }

    /// <summary>
    /// Gets the response associated with the post-processing context.
    /// This property may be null if the response is not available or not yet created.
    /// </summary>
    public TResponse? Response { get; init; }

    /// <summary>
    /// Gets the <see cref="HttpContext"/> associated with the current request and response.
    /// This property is required and must be initialized.
    /// </summary>
    public HttpContext HttpContext { get; init; }

    /// <summary>
    /// Gets a collection of <see cref="ValidationFailure"/> instances that describe any validation failures.
    /// This property is required and must be initialized.
    /// </summary>
    public IReadOnlyCollection<ValidationFailure> ValidationFailures { get; init; }

    /// <summary>
    /// Gets the <see cref="ExceptionDispatchInfo"/> if an exception was captured during the processing.
    /// This property may be null if no exception was captured.
    /// </summary>
    public ExceptionDispatchInfo? ExceptionDispatchInfo { get; init; }
}
