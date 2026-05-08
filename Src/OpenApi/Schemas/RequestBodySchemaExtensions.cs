using System.Reflection;
using System.Text.Json;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static partial class OperationSchemaHelpers
{
    internal static void RemovePropFromRequestBody(this OpenApiOperation operation,
                                                   PropertyInfo property,
                                                   SharedContext sharedCtx,
                                                   string operationKey,
                                                   DocumentOptions docOpts,
                                                   JsonNamingPolicy? namingPolicy,
                                                   HashSet<string>? removedProps = null)
    {
        if (operation.RequestBody?.Content is null)
            return;

        var schemaName = PropertyNameResolver.GetSchemaPropertyName(property, namingPolicy, docOpts.UsePropertyNamingPolicy);

        removedProps?.Add(schemaName);

        foreach (var content in operation.RequestBody.Content.Values)
        {
            var schema = content.EnsureOperationLocalSchemaForMutation(sharedCtx, operationKey, "requestBody");

            if (schema?.Properties is null)
                continue;

            var key = schema.Properties.Keys.FindCaseInsensitiveKey(schemaName);

            if (key is not null)
            {
                schema.Properties.Remove(key);
                schema.Required?.Remove(key);
            }
        }
    }

    internal static Type? TryResolveRouteConstraintType(this string rawSegment)
    {
        var colonIdx = rawSegment.IndexOf(':');

        if (colonIdx < 0 || colonIdx == rawSegment.Length - 1)
            return null;

        var tail = rawSegment[(colonIdx + 1)..];
        var constraintName = tail;

        var parenIdx = constraintName.IndexOf('(');
        if (parenIdx >= 0)
            constraintName = constraintName[..parenIdx];

        var nextColonIdx = constraintName.IndexOf(':');
        if (nextColonIdx >= 0)
            constraintName = constraintName[..nextColonIdx];

        constraintName = constraintName.TrimEnd('?');

        return GlobalConfig.RouteConstraintMap.GetValueOrDefault(constraintName);
    }

    internal static bool IsRequestBodyEmpty(this OpenApiOperation operation, SharedContext? sharedCtx = null)
    {
        return operation.RequestBody?.Content is null || operation.RequestBody.Content.Values.All(c => IsContentSchemaEmpty(c.Schema));

        bool IsContentSchemaEmpty(IOpenApiSchema? schema)
        {
            var resolvedSchema = sharedCtx is null ? schema.ResolveSchema() : schema.ResolveSchema(sharedCtx);

            if (resolvedSchema is not { } s)
                return true;

            return s.Type != JsonSchemaType.Array &&
                   s.Type != JsonSchemaType.String &&
                   s.Type != JsonSchemaType.Integer &&
                   s.Type != JsonSchemaType.Number &&
                   s.Type != JsonSchemaType.Boolean &&
                   (s.Properties is null || s.Properties.Count == 0) &&
                   s.AdditionalProperties is null &&
                   s.OneOf is null or { Count: 0 } &&
                   s.AnyOf is null or { Count: 0 } &&
                   s.AllOf is null or { Count: 0 };
        }
    }
}
