using System.Reflection;
using System.Text.Json;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed class OperationParameterNameResolver(DocumentOptions docOpts, SharedContext sharedCtx)
{
    JsonNamingPolicy? NamingPolicy => sharedCtx.NamingPolicy;

    internal string GetEffectiveName(PropertyInfo property, ParameterLocation location)
    {
        var metadata = OperationReflectionCache.GetPropertyMetadata(property);

        return location switch
        {
            ParameterLocation.Header => metadata.FromHeader?.HeaderName ?? ApplyPropertyNamingPolicy(property.Name),
            ParameterLocation.Cookie => metadata.FromCookie?.CookieName ?? ApplyPropertyNamingPolicy(property.Name),
            ParameterLocation.Path => metadata.BindFrom?.Name.ApplyPropNamingPolicy(docOpts, NamingPolicy) ?? ApplyPropertyNamingPolicy(property.Name),
            ParameterLocation.Query => GetQueryName(property),
            _ => property.Name
        };
    }

    internal string GetQueryName(PropertyInfo property)
        => OperationReflectionCache.GetPropertyMetadata(property).BindFrom?.Name ?? ApplyPropertyNamingPolicy(property.Name);

    internal string GetRouteName(string routeParamName)
        => routeParamName.GetOpenApiRouteParameterName(docOpts, NamingPolicy);

    internal string ApplyPropertyNamingPolicy(string name)
        => name.ApplyPropNamingPolicy(docOpts, NamingPolicy);
}
