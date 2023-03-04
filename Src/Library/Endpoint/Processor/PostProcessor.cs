using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// inherit this class to create a post-processor with access to the common processor state of the endpoint
/// </summary>
/// <typeparam name="TRequest">type of the request dto</typeparam>
/// <typeparam name="TState">type of the common processor state</typeparam>
/// <typeparam name="TResponse">type of the response</typeparam>
public abstract class PostProcessor<TRequest, TState, TResponse> : IPostProcessor<TRequest, TResponse> where TState : class, new()
{
    [HideFromDocs]
    public Task PostProcessAsync(TRequest req, TResponse res, HttpContext ctx, IReadOnlyCollection<ValidationFailure> failures, CancellationToken ct)
        => PostProcessAsync(req, ctx.ProcessorState<TState>(), res, ctx, failures, ct);

    /// <summary>
    /// this method is called with the given arguments when the post-processor executes.
    /// </summary>
    /// <param name="req">the request dto object</param>
    /// <param name="state">the common processor state object</param>
    /// <param name="res">the response dto object</param>
    /// <param name="ctx">the http context</param>
    /// <param name="failures">the collection of validation errors of the endpoint</param>
    /// <param name="ct">cancellation token</param>
    public abstract Task PostProcessAsync(TRequest req, TState state, TResponse res, HttpContext ctx, IReadOnlyCollection<ValidationFailure> failures, CancellationToken ct);
}