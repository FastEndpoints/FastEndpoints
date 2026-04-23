using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

/// <summary>
/// populates enum values for integer enum schemas.
/// MS OpenApi generates type: "integer" for enums but does not include the enum array with values.
/// </summary>
sealed class EnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        var type = context.JsonTypeInfo.Type;

        if (!type.IsEnum)
            return Task.CompletedTask;

        if (!schema.Type.HasValue || !schema.Type.Value.HasFlag(JsonSchemaType.Integer))
            return Task.CompletedTask;

        var values = Enum.GetValuesAsUnderlyingType(type);
        var isUnsignedLong = Enum.GetUnderlyingType(type) == typeof(ulong);

        schema.Enum ??= [];

        foreach (var val in values)
            schema.Enum.Add(isUnsignedLong
                                ? JsonValue.Create(Convert.ToDecimal(val))
                                : JsonValue.Create(Convert.ToInt64(val)));

        return Task.CompletedTask;
    }
}
