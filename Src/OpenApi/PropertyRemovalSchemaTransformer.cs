using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

abstract class PropertyRemovalSchemaTransformer(DocumentOptions docOpts, SharedContext sharedCtx) : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        if (context.JsonPropertyInfo is not null || schema.Properties is not { Count: > 0 })
            return Task.CompletedTask;

        var namingPolicy = sharedCtx.ResolveNamingPolicy();

        foreach (var prop in GetPropertiesToRemove(context.JsonTypeInfo.Type))
        {
            var jsonName = PropertyNameResolver.GetSchemaPropertyName(prop, namingPolicy, docOpts.UsePropertyNamingPolicy);

            schema.Properties.Remove(jsonName);
            schema.Required?.Remove(jsonName);
        }

        return Task.CompletedTask;
    }

    protected abstract IReadOnlyList<PropertyInfo> GetPropertiesToRemove(Type type);
}
