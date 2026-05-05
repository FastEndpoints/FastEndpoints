using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

/// <summary>
/// populates enum values using the effective FastEndpoints serializer options.
/// </summary>
sealed class EnumSchemaTransformer(SharedContext sharedCtx) : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        var type = context.JsonTypeInfo.Type.GetUnderlyingType();

        if (!type.IsEnum)
            return Task.CompletedTask;

        var values = Enum.GetValues(type);
        var serializerOptions = sharedCtx.ResolveSerializerOptions(context.ApplicationServices);

        schema.Enum = [];

        foreach (var val in values)
        {
            var enumValue = JsonSerializer.SerializeToNode(val, type, serializerOptions);

            if (enumValue is not null)
                schema.Enum.Add(enumValue);
        }

        if (schema.Enum.Count == 0)
            return Task.CompletedTask;

        schema.Type = schema.Enum[0] switch
        {
            JsonValue jv when jv.TryGetValue(out string? _) => JsonSchemaType.String,
            JsonValue jv when jv.TryGetValue(out bool _) => JsonSchemaType.Boolean,
            _ => JsonSchemaType.Integer
        };

        if (schema.Type.Value.HasFlag(JsonSchemaType.String))
            schema.Format = null;

        return Task.CompletedTask;
    }
}