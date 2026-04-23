using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

/// <summary>
/// removes properties decorated with [ToHeader] from object schemas.
/// these properties are sent as response headers, not in the JSON body,
/// so they should not appear in the schema.
/// </summary>
sealed class ToHeaderPropertySchemaTransformer(SharedContext sharedCtx) : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        var namingPolicy = sharedCtx.ResolveNamingPolicy(context.ApplicationServices);

        // only process type-level schemas (not property-level)
        if (context.JsonPropertyInfo is not null || schema.Properties is not { Count: > 0 })
            return Task.CompletedTask;

        var type = context.JsonTypeInfo.Type;

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.IsDefined(typeof(ToHeaderAttribute), true))
                continue;

            var jsonName = PropertyNameResolver.GetSchemaPropertyName(prop, namingPolicy);

            schema.Properties.Remove(jsonName);
            schema.Required?.Remove(jsonName);
        }

        return Task.CompletedTask;
    }
}
