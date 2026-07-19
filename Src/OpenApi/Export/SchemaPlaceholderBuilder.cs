using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

/// <summary>
/// turns an OpenAPI schema into a placeholder JSON value keyed by property name,
/// so generated request bodies are fillable rather than an opaque '{}'.
/// </summary>
static class SchemaPlaceholderBuilder
{
    // always resolve $ref (Microsoft.OpenApi 2.0 OpenApiSchemaReference often has null Properties/Type until Target/components are resolved).
    // 'seen' tracks the resolved schemas on the CURRENT recursion path (not every schema ever visited), so a DTO referenced twice in
    // sibling branches (e.g. the same Address type used by both 'billingAddress' and 'shippingAddress') is still walked properly -
    // only a schema that recurses into itself is short-circuited.
    internal static JsonNode? Build(IOpenApiSchema schema, HashSet<IOpenApiSchema> seen, IDictionary<string, IOpenApiSchema>? components)
    {
        var resolved = schema.ResolveSchema(components);

        if (resolved is null)
            return null; // unresolved ref with no concrete fallback

        if (!seen.Add(resolved))
            return null; // true cycle: this schema is already being walked further up the same path

        try
        {
            // prefer document-authored sample data over type placeholders (media-type examples are handled by the exporter)
            if (resolved.Example is not null)
                return resolved.Example.DeepClone();

            if (resolved.Default is not null)
                return resolved.Default.DeepClone();

            if (resolved.OneOf?.Count > 0 || resolved.AnyOf?.Count > 0 || resolved.AllOf?.Count > 0)
            {
                // the common "nullable $ref" idiom is encoded as a oneOf/anyOf with exactly one non-null branch - that's not
                // real polymorphism, so unwrap it and walk the referenced schema instead of falling back to an empty object.
                var branches = resolved.OneOf ?? resolved.AnyOf ?? resolved.AllOf!;
                var nonNullBranches = branches.Where(b => !IsNullSchema(b, components)).ToList();

                if (nonNullBranches.Count == 1)
                    return Build(nonNullBranches[0], seen, components);

                return new JsonObject(); // genuinely composed/polymorphic schema, fall back to an empty object
            }

            var type = resolved.Type;

            if (type?.HasFlag(JsonSchemaType.Array) == true)
            {
                return resolved.Items is null
                           ? new JsonArray()
                           : new JsonArray(Build(resolved.Items, seen, components));
            }

            if (resolved.Properties is { Count: > 0 } props && (type is null || type.Value.HasFlag(JsonSchemaType.Object)))
            {
                var obj = new JsonObject();

                foreach (var (name, propSchema) in props)
                    obj[name] = Build(propSchema, seen, components);

                return obj;
            }

            if (type?.HasFlag(JsonSchemaType.String) == true)
            {
                return resolved.Enum?.FirstOrDefault() is JsonValue enumValue && enumValue.TryGetValue<string>(out var s)
                           ? JsonValue.Create(s)
                           : JsonValue.Create("");
            }

            if (type?.HasFlag(JsonSchemaType.Integer) == true || type?.HasFlag(JsonSchemaType.Number) == true)
                return JsonValue.Create(0);

            if (type?.HasFlag(JsonSchemaType.Boolean) == true)
                return JsonValue.Create(false);

            return type?.HasFlag(JsonSchemaType.Object) == true ? new JsonObject() : null;
        }
        finally
        {
            seen.Remove(resolved);
        }
    }

    static bool IsNullSchema(IOpenApiSchema schema, IDictionary<string, IOpenApiSchema>? components)
    {
        var resolved = schema.ResolveSchema(components);

        return resolved?.Type == JsonSchemaType.Null ||
               (resolved is null && schema.Type == JsonSchemaType.Null);
    }
}
