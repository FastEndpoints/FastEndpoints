using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace FastEndpoints.OpenApi;

// ASP.NET Core OpenAPI generates schemas from Http.Json options, while FastEndpoints configures
// a separate serializer options copy during endpoint mapping. Bridge missing metadata lookups
// back to FastEndpoints so live MapOpenApi() can see generated and endpoint-local contexts.

sealed class FeOpenApiJsonOptions : IPostConfigureOptions<JsonOptions>
{
    public void PostConfigure(string? name, JsonOptions options)
    {
        var chain = options.SerializerOptions.TypeInfoResolverChain;

        if (!chain.Contains(FastEndpointsOpenApiJsonTypeInfoResolver.Instance))
            chain.Insert(0, FastEndpointsOpenApiJsonTypeInfoResolver.Instance);
    }
}

sealed class FastEndpointsOpenApiJsonTypeInfoResolver : IJsonTypeInfoResolver
{
    public static readonly FastEndpointsOpenApiJsonTypeInfoResolver Instance = new();

    [ThreadStatic] static HashSet<Type>? _resolvingTypes;

    FastEndpointsOpenApiJsonTypeInfoResolver() { }

    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        // Cfg.SerOpts can contain this resolver because it is copied from Http.Json during endpoint mapping.
        var resolvingTypes = _resolvingTypes ??= [];

        if (!resolvingTypes.Add(type))
            return null;

        var resolver = Cfg.SerOpts.Options.TypeInfoResolver;

        if (resolver is null)
            return null;

        try
        {
            return resolver.GetTypeInfo(type, options);
        }
        finally
        {
            resolvingTypes.Remove(type);

            if (resolvingTypes.Count == 0)
                _resolvingTypes = null;
        }
    }
}