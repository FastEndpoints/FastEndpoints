namespace FastEndpoints;

/// <summary>
/// defines the interface for a pre-processor that can perform asynchronous pre-processing tasks before a request has been handled.
/// </summary>
public interface IPreProcessor
{
    /// <summary>
    /// asynchronously performs pre-processing on the provided context.
    /// </summary>
    /// <param name="context">the pre-processor context containing request, and other processing details.</param>
    /// <param name="ct">the <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>a <see cref="Task" /> that represents the asynchronous pre-process operation.</returns>
    Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct);
}

/// <summary>
/// defines the generic interface for a pre-processor with specific types for the request,
/// enabling type-safe pre-processing.
/// </summary>
/// <typeparam name="TRequest">the type of the request object, which must be non-nullable.</typeparam>
public interface IPreProcessor<in TRequest> : IPreProcessor
{
    /// <summary>
    /// explicit interface method implementation for <see cref="IPreProcessor" />.
    /// converts the non-generic context to a generic context and calls the generic PreProcessAsync method.
    /// </summary>
    /// <param name="context">the non-generic pre-processor context.</param>
    /// <param name="ct">the cancellation token.</param>
    /// <returns>the task resulting from the asynchronous operation.</returns>
    Task IPreProcessor.PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
        => PreProcessAsync((IPreProcessorContext<TRequest>)context, ct);

    /// <summary>
    /// asynchronously performs pre-processing on the provided context with a specific request type.
    /// </summary>
    /// <param name="context">the pre-processor context containing the typed request, and other processing details.</param>
    /// <param name="ct">The <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>a <see cref="Task" /> that represents the asynchronous pre-process operation.</returns>
    Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct);
}

/// <summary>
/// interface for defining global pre-processors to be executed before the main endpoint handler is called
/// </summary>
public interface IGlobalPreProcessor : IPreProcessor;