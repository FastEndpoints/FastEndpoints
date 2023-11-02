using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// Inherit this class to create a post-processor with access to the common processor state of the endpoint.
/// </summary>
/// <typeparam name="TRequest">Type of the request DTO.</typeparam>
/// <typeparam name="TState">Type of the common processor state.</typeparam>
/// <typeparam name="TResponse">Type of the response.</typeparam>
public abstract class PostProcessor<TRequest, TState, TResponse> : IPostProcessor<TRequest, TResponse>
    where TState : class, new()
{
    /// <summary>
    /// This method is called internally to prepare the state and invoke the abstract PostProcessAsync method.
    /// It's not intended for direct external use.
    /// </summary>
    /// <param name="context">The context containing the request, response, HttpContext, validation failures, and any exception information.</param>
    /// <param name="ct">Cancellation token.</param>
    [HideFromDocs]
    public Task PostProcessAsync(PostProcessorContext<TRequest, TResponse> context, CancellationToken ct)
        => PostProcessAsync(context, context.HttpContext.ProcessorState<TState>(), ct);

    /// <summary>
    /// Implement this method to define the post-processing logic using the provided context and state.
    /// </summary>
    /// <param name="context">The context object encapsulating all necessary information for post-processing.</param>
    /// <param name="state">The common processor state object, derived from the HttpContext or newly instantiated.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public abstract Task PostProcessAsync(PostProcessorContext<TRequest, TResponse> context,
                                          TState state,
                                          CancellationToken ct);
}