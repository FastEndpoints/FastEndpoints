namespace FastEndpoints;

/// <summary>
/// Defines the interface for a pre-processor that can perform asynchronous pre-processing tasks
/// before a request has been handled.
/// </summary>
public interface IPreProcessor
{
    /// <summary>
    /// Asynchronously performs pre-processing on the provided context.
    /// </summary>
    /// <param name="context">The pre-processor context containing request, and other processing details.</param>
    /// <param name="ct">The <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous pre-process operation.</returns>
    Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct);
}

/// <summary>
/// Defines the generic interface for a pre-processor with specific types for the request,
/// enabling type-safe pre-processing.
/// </summary>
/// <typeparam name="TRequest">The type of the request object, which must be non-nullable.</typeparam>
public interface IPreProcessor<in TRequest> : IPreProcessor
    where TRequest : notnull
{
    /// <summary>
    /// Explicit interface method implementation for <see cref="IPreProcessor"/>.
    /// Converts the non-generic context to a generic context and calls the generic PreProcessAsync method.
    /// </summary>
    /// <param name="context">The non-generic pre-processor context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The task resulting from the asynchronous operation.</returns>
    Task IPreProcessor.PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
        => PreProcessAsync((IPreProcessorContext<TRequest>)context, ct);

    /// <summary>
    /// Asynchronously performs pre-processing on the provided context with a specific request type.
    /// </summary>
    /// <param name="context">The pre-processor context containing the typed request, and other processing details.</param>
    /// <param name="ct">The <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous pre-process operation.</returns>
    Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct);
}

/// <summary>
/// interface for defining global pre-processors to be executed before the main endpoint handler is called
/// </summary>
public interface IGlobalPreProcessor : IPreProcessor { }