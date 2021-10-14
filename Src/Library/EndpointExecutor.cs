using FastEndpoints.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Reflection;

namespace FastEndpoints;

internal record EndpointDefinitionCacheEntry(
    Func<object> CreateInstance,
    IValidator? Validator,
    object? PreProcessors,
    object? PostProcessors);

[HideFromDocs]
public static class EndpointExecutor
{
    //key: route url for the endpoint
    internal static Dictionary<string, EndpointDefinitionCacheEntry> CachedEndpointDefinitions { get; } = new();

    //key: TEndpoint
    internal static Dictionary<Type, PropertyInfo[]> CachedServiceBoundProps { get; } = new();

    //note: this handler is called by .net for each http request
    public static Task HandleAsync(HttpContext ctx, CancellationToken cancellation)
    {
        var ep = ((RouteEndpoint?)ctx.GetEndpoint());
        var route = ep?.RoutePattern.RawText; if (route == null) throw new InvalidOperationException("Unable to instantiate endpoint!!!");
        var epDef = CachedEndpointDefinitions[route];
        var endpointInstance = epDef.CreateInstance();

        var respCacheAttrib = ep?.Metadata.OfType<ResponseCacheAttribute>().FirstOrDefault();
        if (respCacheAttrib is not null)
            ResponseCacheExecutor.Execute(ctx, respCacheAttrib);

        ResolveServices(endpointInstance, ctx);

#pragma warning disable CS8601
        return (Task?)BaseEndpoint.ExecMethodInfo.Invoke(endpointInstance, new object[] { ctx, epDef.Validator, epDef.PreProcessors, epDef.PostProcessors, cancellation })
            ?? Task.CompletedTask;
#pragma warning restore CS8601
    }

    private static void ResolveServices(object endpointInstance, HttpContext ctx)
    {
        if (CachedServiceBoundProps.TryGetValue(endpointInstance.GetType(), out var props))
        {
            for (int i = 0; i < props.Length; i++)
            {
                PropertyInfo? prop = props[i];
                var serviceInstance = ctx.RequestServices.GetService(prop.PropertyType);
                prop.SetValue(endpointInstance, serviceInstance);
            }
        }
    }
}

