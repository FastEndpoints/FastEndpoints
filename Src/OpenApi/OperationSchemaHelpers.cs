using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static partial class OperationSchemaHelpers
{
    internal static OpenApiSchema StringSchema()
        => new() { Type = JsonSchemaType.String };

    internal static void RemoveProperties(this JsonObject obj, IEnumerable<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var key = obj.Select(kvp => kvp.Key).FindCaseInsensitiveKey(propertyName);
            if (key is not null)
                obj.Remove(key);
        }
    }

    internal static string? FindCaseInsensitiveKey(this IEnumerable<string> keys, string match)
        => keys.FirstOrDefault(k => string.Equals(k, match, StringComparison.OrdinalIgnoreCase));

    internal static OpenApiSchema? CreateSchemaFromExampleNode(JsonNode? node)
        => node switch
        {
            JsonObject obj => new()
            {
                Type = JsonSchemaType.Object,
                Properties = obj.ToDictionary(
                    kvp => kvp.Key,
                    IOpenApiSchema (kvp) => CreateSchemaFromExampleNode(kvp.Value) ?? StringSchema())
            },
            JsonArray arr => new()
            {
                Type = JsonSchemaType.Array,
                Items = CreateSchemaFromExampleNode(arr.FirstOrDefault()) ?? StringSchema()
            },
            JsonValue value => CreatePrimitiveSchemaFromValue(value),
            _ => null
        };

    static OpenApiSchema CreatePrimitiveSchemaFromValue(JsonValue value)
    {
        if (value.TryGetValue<bool>(out _))
            return new() { Type = JsonSchemaType.Boolean };

        if (value.TryGetValue<int>(out _))
            return new() { Type = JsonSchemaType.Integer, Format = "int32" };

        if (value.TryGetValue<long>(out _))
            return new() { Type = JsonSchemaType.Integer, Format = "int64" };

        if (value.TryGetValue<decimal>(out _))
            return new() { Type = JsonSchemaType.Number };

        return StringSchema();
    }
}
