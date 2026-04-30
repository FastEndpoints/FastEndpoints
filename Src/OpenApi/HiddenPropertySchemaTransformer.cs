using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed class HiddenPropertySchemaTransformer(DocumentOptions docOpts, SharedContext sharedCtx) : IOpenApiSchemaTransformer
{
    static readonly ConcurrentDictionary<Type, PropertyInfo[]> _hiddenPropertiesCache = new();

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        var namingPolicy = sharedCtx.ResolveNamingPolicy(context.ApplicationServices);

        if (context.JsonPropertyInfo is not null || schema.Properties is not { Count: > 0 })
            return Task.CompletedTask;

        foreach (var prop in _hiddenPropertiesCache.GetOrAdd(context.JsonTypeInfo.Type, GetHiddenProperties))
        {
            var jsonName = PropertyNameResolver.GetSchemaPropertyName(prop, namingPolicy, docOpts.UsePropertyNamingPolicy);

            schema.Properties.Remove(jsonName);
            schema.Required?.Remove(jsonName);
        }

        return Task.CompletedTask;
    }

    static PropertyInfo[] GetHiddenProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
               .Where(static prop => prop.IsDefined(Types.HideFromDocsAttribute))
               .ToArray();
}
