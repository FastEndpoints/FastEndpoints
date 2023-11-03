namespace FastEndpoints;

/// <summary>
/// inherit this class to create a pre-processor with access to the common processor state of the endpoint
/// </summary>
/// <typeparam name="TRequest">type of the request dto</typeparam>
/// <typeparam name="TState">type of the common processor state</typeparam>
public abstract class PreProcessor<TRequest, TState> : IPreProcessor<TRequest>
    where TState : class, new()
    where TRequest : notnull
{
    /// <summary>
    /// not intended for direct external use.
    /// </summary>
    [HideFromDocs]
    public Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct)
        => PreProcessAsync(context, context.HttpContext.ProcessorState<TState>(), ct);

    // ReSharper disable once MemberCanBeProtected.Global
    /// <summary>
    /// this method is called with the given arguments when the pre-processor executes.
    /// </summary>
    /// <param name="context">the context object encapsulating all necessary information for pre-processing.</param>
    /// <param name="state">the common processor state object</param>
    /// <param name="ct">cancellation token</param>
    public abstract Task PreProcessAsync(IPreProcessorContext<TRequest> context, TState state, CancellationToken ct);
}