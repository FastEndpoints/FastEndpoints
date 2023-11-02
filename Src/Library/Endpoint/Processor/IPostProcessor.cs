namespace FastEndpoints;

/// <summary>
/// Defines the interface for a post-processor that can perform asynchronous post-processing tasks
/// after a request has been handled.
/// </summary>
public interface IPostProcessor
{
    /// <summary>
    /// Asynchronously performs post-processing on the provided context.
    /// </summary>
    /// <param name="context">The post-processor context containing request, response, and other processing details.</param>
    /// <param name="ct">The <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous post-process operation.</returns>
    Task PostProcessAsync(IPostProcessorContext context, CancellationToken ct);
}

/// <summary>
/// Defines the generic interface for a post-processor with specific types for the request and response,
/// enabling type-safe post-processing.
/// </summary>
/// <typeparam name="TRequest">The type of the request object, which must be non-nullable.</typeparam>
/// <typeparam name="TResponse">The type of the response object.</typeparam>
public interface IPostProcessor<in TRequest, in TResponse> : IPostProcessor
    where TRequest : notnull
{
    /// <summary>
    /// Explicit interface method implementation for <see cref="IPostProcessor"/>.
    /// Converts the non-generic context to a generic context and calls the generic PostProcessAsync method.
    /// </summary>
    /// <param name="context">The non-generic post-processor context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The task resulting from the asynchronous operation.</returns>
    Task IPostProcessor.PostProcessAsync(IPostProcessorContext context, CancellationToken ct)
        => PostProcessAsync((IPostProcessorContext<TRequest, TResponse>)context, ct);

    /// <summary>
    /// Asynchronously performs post-processing on the provided context with specific request and response types.
    /// </summary>
    /// <param name="context">The post-processor context containing the typed request, response, and other processing details.</param>
    /// <param name="ct">The <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous post-process operation.</returns>
    Task PostProcessAsync(IPostProcessorContext<TRequest, TResponse> context, CancellationToken ct);
}


/// <summary>
/// interface for defining global post-processors to be executed after the main endpoint handler is done
/// </summary>
public interface IGlobalPostProcessor : IPostProcessor { }