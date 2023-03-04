using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

public static class HttpContextExtensions
{
    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    public static TService? TryResolve<TService>(this HttpContext _) where TService : class
        => Config.ServiceResolver.TryResolve<TService>();

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    public static object? TryResolve(this HttpContext _, Type typeOfService)
        => Config.ServiceResolver.TryResolve(typeOfService);

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public static TService Resolve<TService>(this HttpContext _) where TService : class
        => Config.ServiceResolver.Resolve<TService>();

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public static object Resolve(this HttpContext _, Type typeOfService)
        => Config.ServiceResolver.Resolve(typeOfService);

    /// <summary>
    /// marks the current response as started so that <see cref="ResponseStarted(HttpContext)"/> can return the correct result.
    /// </summary>
    /// <param name="ctx"></param>
    public static void MarkResponseStart(this HttpContext ctx)
        => ctx.Items[CtxKey.ResponseStarted] = null;

    /// <summary>
    /// check if the current response has already started or not.
    /// </summary>
    public static bool ResponseStarted(this HttpContext ctx)
        => ctx.Response.HasStarted || ctx.Items.ContainsKey(CtxKey.ResponseStarted);

    /// <summary>
    /// retrieve the common processor state for the current http context.
    /// </summary>
    /// <typeparam name="TState">the type of the processor state</typeparam>
    /// <exception cref="InvalidOperationException">thrown if the requested type of the processor state does not match with what's already stored in the context</exception>
    public static TState ProcessorState<TState>(this HttpContext ctx) where TState : class, new()
    {
        if (ctx.Items.TryGetValue(CtxKey.ProcessorState, out var state))
            return state as TState ?? throw new InvalidOperationException($"Only a single type of state is supported across processors and endpoint handler! Requested: [{typeof(TState).Name}] Found: [{state!.GetType().Name}]");

        var st = new TState();
        ctx.Items[CtxKey.ProcessorState] = st;
        return st;
    }
}

internal static class CtxKey
{
    internal const int ResponseStarted = 0;
    internal const int ValidationFailures = 1;
    internal const int ProcessorState = 2;
}