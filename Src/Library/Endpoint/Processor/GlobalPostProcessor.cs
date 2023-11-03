namespace FastEndpoints;

/// <summary>
/// inherit this class to create a global post-processor with access to the common processor state of the endpoint
/// </summary>
/// <typeparam name="TState">type of the common processor state</typeparam>
public abstract class GlobalPostProcessor<TState> : IGlobalPostProcessor where TState : class, new()
{
    /// <summary>
    /// not intended for direct external use.
    /// </summary>
    [HideFromDocs]
    public Task PostProcessAsync(IPostProcessorContext context, CancellationToken ct)
        => PostProcessAsync(context, context.HttpContext.ProcessorState<TState>(), ct);

    // ReSharper disable once MemberCanBeProtected.Global
    /// <summary>
    /// implement this method to define the post-processing logic using the provided context and state.
    /// </summary>
    /// <param name="context">the context object encapsulating all necessary information for post-processing.</param>
    /// <param name="state">the common processor state object, derived from the HttpContext or newly instantiated.</param>
    /// <param name="ct">cancellation token.</param>
    /// <returns>a Task representing the asynchronous operation.</returns>
    public abstract Task PostProcessAsync(IPostProcessorContext context, TState state, CancellationToken ct);
}