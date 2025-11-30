using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

public static class HttpContextExtensions
{
    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <param name="keyName">the key name to resolve a keyed service</param>
    public static TService? TryResolve<TService>(this HttpContext _, string keyName) where TService : class
        => ServiceResolver.Instance.TryResolve<TService>(keyName);

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <param name="keyName">the key name to resolve a keyed service</param>
    public static object? TryResolve(this HttpContext _, Type typeOfService, string keyName)
        => ServiceResolver.Instance.TryResolve(typeOfService, keyName);

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <param name="keyName">the key name to resolve a keyed service</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public static TService Resolve<TService>(this HttpContext _, string keyName) where TService : class
        => ServiceResolver.Instance.Resolve<TService>(keyName);

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <param name="keyName">the key name to resolve a keyed service</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public static object Resolve(this HttpContext _, Type typeOfService, string keyName)
        => ServiceResolver.Instance.Resolve(typeOfService, keyName);

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    public static TService? TryResolve<TService>(this HttpContext _) where TService : class
        => ServiceResolver.Instance.TryResolve<TService>();

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    public static object? TryResolve(this HttpContext _, Type typeOfService)
        => ServiceResolver.Instance.TryResolve(typeOfService);

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public static TService Resolve<TService>(this HttpContext _) where TService : class
        => ServiceResolver.Instance.Resolve<TService>();

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public static object Resolve(this HttpContext _, Type typeOfService)
        => ServiceResolver.Instance.Resolve(typeOfService);

    /// <summary>
    /// marks the current response as started so that <see cref="ResponseStarted(HttpContext)" /> can return the correct result.
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
    /// <exception cref="InvalidOperationException">
    /// thrown if the requested type of the processor state does not match with what's already stored in the
    /// context
    /// </exception>
    public static TState ProcessorState<TState>(this HttpContext ctx) where TState : class, new()
    {
        if (ctx.Items.TryGetValue(CtxKey.ProcessorState, out var state))
        {
            return state as TState ??
                   throw new InvalidOperationException(
                       $"""
                        Only a single type of state is supported across processors and endpoint handler! Requested: [{typeof(TState).Name}]
                        Found: [{state!.GetType().Name}]
                        """);
        }

        var st = new TState();
        ctx.Items[CtxKey.ProcessorState] = st;

        return st;
    }

    /// <summary>
    /// adds headers to the http response by reading response dto properties decorated with the [ToHeader(...)] attribute
    /// </summary>
    /// <param name="response">the response dto instance</param>
    public static void PopulateResponseHeadersFromResponseDto(this HttpContext ctx, object? response)
    {
        var toHeaderProps = ctx.Items[CtxKey.ToHeaderProps] as ToHeaderProp[] ?? [];

        for (var i = 0; i < toHeaderProps.Length; i++)
        {
            var p = toHeaderProps[i];
            ctx.Response.Headers[p.HeaderName] = p.PropGetter?.Invoke(response!)?.ToString();
        }
    }

    internal static void MarkEdiHandled(this HttpContext ctx)
        => ctx.Items[CtxKey.EdiIsHandled] = null;

    internal static bool EdiIsHandled(this HttpContext ctx)
        => ctx.Items.ContainsKey(CtxKey.EdiIsHandled);

    internal static void StoreResponse(this HttpContext ctx, object? response)
        => ctx.Items[Constants.FastEndpointsResponse] = response;
}

static class CtxKey
{
    //values are strings to avoid boxing when doing dictionary lookups in HttpContext.Items
    internal const string ResponseStarted = "0";
    internal const string ValidationFailures = "1";
    internal const string ProcessorState = "2";
    internal const string EdiIsHandled = "3";
    internal const string ToHeaderProps = "4";
}