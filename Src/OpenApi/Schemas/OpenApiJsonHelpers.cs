using System.Text.Json;
using System.Text.Json.Nodes;

namespace FastEndpoints.OpenApi;

static partial class OperationSchemaHelpers
{
    internal static JsonNode? ParseXmlExampleJsonNode(string? example, bool preserveRawString = false)
    {
        if (example is null)
            return null;

        try
        {
            return JsonNode.Parse(example);
        }
        catch
        {
            return preserveRawString ? JsonValue.Create(example) : null;
        }
    }

    extension(object? value)
    {
        internal JsonNode? JsonNodeFromObject(JsonSerializerOptions serializerOptions)
        {
            if (value is null)
                return null;

            return TrySerializeToNode(value, serializerOptions: serializerOptions);
        }

        internal JsonObject? JsonObjectFromObject(JsonSerializerOptions serializerOptions, Type? valueType = null)
        {
            if (value is null)
                return null;

            return TrySerializeToNode(value, valueType, serializerOptions) as JsonObject;
        }
    }

    static JsonNode? TrySerializeToNode(object value, Type? valueType = null, JsonSerializerOptions? serializerOptions = null)
    {
        try
        {
            serializerOptions ??= Cfg.SerOpts.Options;

            return valueType is null
                       ? JsonSerializer.SerializeToNode(value, serializerOptions)
                       : JsonSerializer.SerializeToNode(value, valueType, serializerOptions);
        }
        catch
        {
            return null;
        }
    }
}
