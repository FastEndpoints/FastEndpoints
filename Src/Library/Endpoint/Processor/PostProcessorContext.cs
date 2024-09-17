using System.Runtime.ExceptionServices;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

#pragma warning disable CS8618

namespace FastEndpoints;

/// <summary>
/// represents the context for a post-processing operation with a request and response pair.
/// </summary>
/// <typeparam name="TRequest">the type of the request object, which must be non-nullable.</typeparam>
/// <typeparam name="TResponse">the type of the response object.</typeparam>
public sealed class PostProcessorContext<TRequest, TResponse> : IPostProcessorContext<TRequest, TResponse>
{
    /// <summary>
    /// gets the request associated with the post-processing context.
    /// may be null if request binding has failed.
    /// </summary>
    public TRequest? Request { get; init; }

    /// <summary>
    /// gets the response associated with the post-processing context.
    /// may be null if the response is not available or not yet created.
    /// </summary>
    public TResponse? Response { get; init; }

    /// <summary>
    /// gets the <see cref="HttpContext" /> associated with the current request and response.
    /// </summary>
    public HttpContext HttpContext { get; init; }

    /// <summary>
    /// gets a collection of <see cref="ValidationFailure" /> instances that describe any validation failures.
    /// </summary>
    public IReadOnlyCollection<ValidationFailure> ValidationFailures { get; init; }

    /// <summary>
    /// gets the <see cref="ExceptionDispatchInfo" /> if an exception was captured during the processing.
    /// may be null if no exception was captured.
    /// </summary>
    public ExceptionDispatchInfo? ExceptionDispatchInfo { get; init; }

    internal PostProcessorContext(TRequest? request,
                                  TResponse? response,
                                  HttpContext httpContext,
                                  ExceptionDispatchInfo? exceptionDispatchInfo,
                                  IReadOnlyCollection<ValidationFailure> validationFailures)
    {
        Request = request;
        Response = response;
        HttpContext = httpContext;
        ExceptionDispatchInfo = exceptionDispatchInfo;
        ValidationFailures = validationFailures;
    }
}