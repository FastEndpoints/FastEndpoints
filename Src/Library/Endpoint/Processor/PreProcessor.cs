using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// inherit this class to create a pre-processor with access to the common processor state of the endpoint
/// </summary>
/// <typeparam name="TRequest">type of the request dto</typeparam>
/// <typeparam name="TState">type of the common processor state</typeparam>
public abstract class PreProcessor<TRequest, TState> : IPreProcessor<TRequest> where TState : class, new()
{
    [HideFromDocs]
    public Task PreProcessAsync(TRequest req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct)
        => PreProcessAsync(req, ctx.ProcessorState<TState>(), ctx, failures, ct);

    /// <summary>
    /// this method is called with the given arguments when the pre-processor executes.
    /// </summary>
    /// <param name="req">the request dto object</param>
    /// <param name="state">the common processor state object</param>
    /// <param name="ctx">the http context</param>
    /// <param name="failures">the collection of validation errors of the endpoint</param>
    /// <param name="ct">cancellation token</param>
    public abstract Task PreProcessAsync(TRequest req, TState state, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct);
}
