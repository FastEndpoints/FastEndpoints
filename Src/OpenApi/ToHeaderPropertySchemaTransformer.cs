using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

/// <summary>
/// removes properties decorated with [ToHeader] from object schemas.
/// these properties are sent as response headers, not in the JSON body,
/// so they should not appear in the schema.
/// </summary>
sealed class ToHeaderPropertySchemaTransformer(DocumentOptions docOpts, SharedContext sharedCtx) : IOpenApiSchemaTransformer
{
    static readonly ConcurrentDictionary<Type, PropertyInfo[]> _toHeaderPropertiesCache = new();

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        // only process type-level schemas (not property-level)
        if (context.JsonPropertyInfo is not null || schema.Properties is not { Count: > 0 })
            return Task.CompletedTask;

        var namingPolicy = sharedCtx.ResolveNamingPolicy(context.ApplicationServices);

        var type = context.JsonTypeInfo.Type;

        foreach (var prop in _toHeaderPropertiesCache.GetOrAdd(type, GetToHeaderProperties))
        {
            var jsonName = PropertyNameResolver.GetSchemaPropertyName(prop, namingPolicy, docOpts.UsePropertyNamingPolicy);

            schema.Properties.Remove(jsonName);
            schema.Required?.Remove(jsonName);
        }

        return Task.CompletedTask;
    }

    static PropertyInfo[] GetToHeaderProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
               .Where(static prop => prop.IsDefined(typeof(ToHeaderAttribute), true))
               .ToArray();
}