namespace FastEndpoints;

/// <summary>
/// inherit this class to create a post-processor with access to the common processor state of the endpoint.
/// </summary>
/// <typeparam name="TRequest">type of the request DTO.</typeparam>
/// <typeparam name="TState">type of the common processor state.</typeparam>
/// <typeparam name="TResponse">type of the response.</typeparam>
public abstract class PostProcessor<TRequest, TState, TResponse> : IPostProcessor<TRequest, TResponse>
    where TState : class, new()
    where TRequest : notnull
{
    /// <summary>
    /// not intended for direct external use.
    /// </summary>
    [HideFromDocs]
    public Task PostProcessAsync(IPostProcessorContext<TRequest, TResponse> context, CancellationToken ct)
        => PostProcessAsync(context, context.HttpContext.ProcessorState<TState>(), ct);

    // ReSharper disable once MemberCanBeProtected.Global
    /// <summary>
    /// implement this method to define the post-processing logic using the provided context and state.
    /// </summary>
    /// <param name="context">the context object encapsulating all necessary information for post-processing.</param>
    /// <param name="state">the common processor state object, derived from the HttpContext or newly instantiated.</param>
    /// <param name="ct">cancellation token.</param>
    /// <returns>a Task representing the asynchronous operation.</returns>
    public abstract Task PostProcessAsync(IPostProcessorContext<TRequest, TResponse> context, TState state, CancellationToken ct);
}