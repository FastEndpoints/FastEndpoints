using Microsoft.AspNetCore.Http;

#pragma warning disable CA1822

namespace FastEndpoints;

public static class HttpContextExtensions
{
    /// <param name="httpCtx"></param>
    extension(HttpContext httpCtx)
    {
        /// <summary>
        /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
        /// </summary>
        /// <typeparam name="TService">the type of the service to resolve</typeparam>
        /// <param name="keyName">the key name to resolve a keyed service</param>
        public TService? TryResolve<TService>(string keyName) where TService : class
            => ServiceResolver.Instance.TryResolve<TService>(keyName);

        /// <summary>
        /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
        /// </summary>
        /// <param name="typeOfService">the type of the service to resolve</param>
        /// <param name="keyName">the key name to resolve a keyed service</param>
        public object? TryResolve(Type typeOfService, string keyName)
            => ServiceResolver.Instance.TryResolve(typeOfService, keyName);

        /// <summary>
        /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
        /// </summary>
        /// <typeparam name="TService">the type of the service to resolve</typeparam>
        /// <param name="keyName">the key name to resolve a keyed service</param>
        /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
        public TService Resolve<TService>(string keyName) where TService : class
            => ServiceResolver.Instance.Resolve<TService>(keyName);

        /// <summary>
        /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
        /// </summary>
        /// <param name="typeOfService">the type of the service to resolve</param>
        /// <param name="keyName">the key name to resolve a keyed service</param>
        /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
        public object Resolve(Type typeOfService, string keyName)
            => ServiceResolver.Instance.Resolve(typeOfService, keyName);

        /// <summary>
        /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
        /// </summary>
        /// <typeparam name="TService">the type of the service to resolve</typeparam>
        public TService? TryResolve<TService>() where TService : class
            => ServiceResolver.Instance.TryResolve<TService>();

        /// <summary>
        /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
        /// </summary>
        /// <param name="typeOfService">the type of the service to resolve</param>
        public object? TryResolve(Type typeOfService)
            => ServiceResolver.Instance.TryResolve(typeOfService);

        /// <summary>
        /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
        /// </summary>
        /// <typeparam name="TService">the type of the service to resolve</typeparam>
        /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
        public TService Resolve<TService>() where TService : class
            => ServiceResolver.Instance.Resolve<TService>();

        /// <summary>
        /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
        /// </summary>
        /// <param name="typeOfService">the type of the service to resolve</param>
        /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
        public object Resolve(Type typeOfService)
            => ServiceResolver.Instance.Resolve(typeOfService);

        /// <summary>
        /// marks the current response as started so that <see cref="ResponseStarted(HttpContext)" /> can return the correct result.
        /// </summary>
        public void MarkResponseStart()
            => httpCtx.Items[CtxKey.ResponseStarted] = null;

        /// <summary>
        /// check if the current response has already started or not.
        /// </summary>
        public bool ResponseStarted()
            => httpCtx.Response.HasStarted || httpCtx.Items.ContainsKey(CtxKey.ResponseStarted);

        /// <summary>
        /// retrieve the common processor state for the current http context.
        /// </summary>
        /// <typeparam name="TState">the type of the processor state</typeparam>
        /// <exception cref="InvalidOperationException">
        /// thrown if the requested type of the processor state does not match with what's already stored in the
        /// context
        /// </exception>
        public TState ProcessorState<TState>() where TState : class, new()
        {
            if (httpCtx.Items.TryGetValue(CtxKey.ProcessorState, out var state))
            {
                return state as TState ??
                       throw new InvalidOperationException(
                           $"""
                            Only a single type of state is supported across processors and endpoint handler! Requested: [{typeof(TState).Name}]
                            Found: [{state!.GetType().Name}]
                            """);
            }

            var st = new TState();
            httpCtx.Items[CtxKey.ProcessorState] = st;

            return st;
        }

        /// <summary>
        /// adds headers to the http response by reading response dto properties decorated with the [ToHeader(...)] attribute
        /// </summary>
        /// <param name="response">the response dto instance</param>
        public void PopulateResponseHeadersFromResponseDto(object? response)
            => httpCtx.PopulateResponseHeadersFromResponseDto(response, null);

        internal void PopulateResponseHeadersFromResponseDto(object? response, ToHeaderProp[]? toHeaderProps)
        {
            toHeaderProps ??= httpCtx.Items[CtxKey.ToHeaderProps] as ToHeaderProp[] ?? [];

            for (var i = 0; i < toHeaderProps.Length; i++)
            {
                var p = toHeaderProps[i];
                httpCtx.Response.Headers[p.HeaderName] = p.PropGetter?.Invoke(response!)?.ToString();
            }
        }

        internal void MarkEdiHandled()
            => httpCtx.Items[CtxKey.EdiIsHandled] = null;

        internal bool EdiIsHandled()
            => httpCtx.Items.ContainsKey(CtxKey.EdiIsHandled);

        internal void StoreResponse(object? response)
            => httpCtx.Items[Constants.FastEndpointsResponse] = response;
    }
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