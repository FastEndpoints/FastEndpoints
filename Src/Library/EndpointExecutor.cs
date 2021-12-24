using FastEndpoints.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

[HideFromDocs]
public static class EndpointExecutor
{
    //key: endpoint route with verb prefixed - {verb}:{path/of/route}
    internal static Dictionary<string, EndpointDefinitionCacheEntry> CachedEndpointDefinitions { get; } = new();

    //key: TEndpoint
    internal static Dictionary<Type, ServiceBoundPropCacheEntry[]> CachedServiceBoundProps { get; } = new();

    //note: this handler is called by .net for each http request
    public static Task HandleAsync(HttpContext ctx, CancellationToken cancellation)
    {
        var ep = (RouteEndpoint?)ctx.GetEndpoint();
        var routePath = ep?.RoutePattern.RawText;
        var epDef = CachedEndpointDefinitions[$"{ctx.Request.Method}:{routePath}"];
        var endpointInstance = (BaseEndpoint)epDef.CreateInstance();

        var respCacheAttrib = ep?.Metadata.OfType<ResponseCacheAttribute>().FirstOrDefault();
        if (respCacheAttrib is not null)
            ResponseCacheExecutor.Execute(ctx, respCacheAttrib);

        ResolveServices(endpointInstance, ctx);

        return endpointInstance.ExecAsync(ctx, epDef.Validator!, epDef.PreProcessors!, epDef.PostProcessors!, cancellation);
    }

    private static void ResolveServices(object endpointInstance, HttpContext ctx)
    {
        if (CachedServiceBoundProps.TryGetValue(endpointInstance.GetType(), out var props))
        {
            for (int i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                var serviceInstance = ctx.RequestServices.GetRequiredService(prop.PropType);
                prop.PropSetter(endpointInstance, serviceInstance!);
            }
        }
    }
}

internal record EndpointDefinitionCacheEntry(
    Func<object> CreateInstance,
    IValidator? Validator,
    object? PreProcessors,
    object? PostProcessors);

internal record ServiceBoundPropCacheEntry(
    Type PropType,
    Action<object, object> PropSetter);

