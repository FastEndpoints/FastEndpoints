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
        internal JsonNode? JsonNodeFromObject()
        {
            if (value is null)
                return null;

            return TrySerializeToNode(value);
        }

        internal JsonObject? JsonObjectFromObject(Type? valueType = null)
        {
            if (value is null)
                return null;

            return TrySerializeToNode(value, valueType) as JsonObject;
        }
    }

    static JsonNode? TrySerializeToNode(object value, Type? valueType = null)
    {
        try
        {
            return valueType is null
                       ? JsonSerializer.SerializeToNode(value, Cfg.SerOpts.Options)
                       : JsonSerializer.SerializeToNode(value, valueType, Cfg.SerOpts.Options);
        }
        catch
        {
            return null;
        }
    }
}