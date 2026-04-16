using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

/// <summary>
/// removes the string union type and regex pattern that MS OpenApi adds to numeric schema properties.
/// MS OpenApi generates type: ["integer","string"] with a pattern for model binding compatibility,
/// but NSwag (and most OpenAPI generators) just use type: "integer".
/// </summary>
sealed class NumericTypeCleanupSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        CleanupNumericType(schema);

        if (schema.Properties is { Count: > 0 })
        {
            foreach (var kvp in schema.Properties)
            {
                if (kvp.Value is OpenApiSchema propSchema)
                    CleanupNumericType(propSchema);
            }
        }

        if (schema.Items is OpenApiSchema itemsSchema)
            CleanupNumericType(itemsSchema);

        return Task.CompletedTask;
    }

    static void CleanupNumericType(OpenApiSchema schema)
    {
        if (schema.Type is not { } type)
            return;

        // check if this is an integer+string or number+string union with a pattern
        var isIntString = type.HasFlag(JsonSchemaType.Integer) && type.HasFlag(JsonSchemaType.String);
        var isNumString = type.HasFlag(JsonSchemaType.Number) && type.HasFlag(JsonSchemaType.String);

        if (!isIntString && !isNumString)
            return;

        // remove the string flag, keep nullable if present
        schema.Type = type & ~JsonSchemaType.String;
        schema.Pattern = null;
    }
}