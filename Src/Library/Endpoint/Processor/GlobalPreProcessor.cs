namespace FastEndpoints;

/// <summary>
/// inherit this class to create a global pre-processor with access to the common processor state of the endpoint
/// </summary>
/// <typeparam name="TState">type of the common processor state</typeparam>
public abstract class GlobalPreProcessor<TState> : IGlobalPreProcessor where TState : class, new()
{
    /// <summary>
    /// not intended for direct external use.
    /// </summary>
    [HideFromDocs]
    public Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
        => PreProcessAsync(context, context.HttpContext.ProcessorState<TState>(), ct);

    // ReSharper disable once MemberCanBeProtected.Global
    /// <summary>
    /// this method is called with the given arguments when the pre-processor executes.
    /// </summary>
    /// <param name="context">the context object encapsulating all necessary information for pre-processing.</param>
    /// <param name="state">the common processor state object</param>
    /// <param name="ct">cancellation token</param>
    public abstract Task PreProcessAsync(IPreProcessorContext context, TState state, CancellationToken ct);
}