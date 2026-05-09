using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints.Agents;

namespace FastEndpoints.Mcp;

static class McpStructuredContentMapper
{
    public static bool TryMap(string text, JsonNode outputSchema, IReadOnlySet<string>? outputPropertyNames, out JsonElement structuredContent)
    {
        if (!TryExtractStructuredContent(text, out var content))
        {
            structuredContent = default;

            return false;
        }

        return TryApplyOutputSchema(content, outputSchema, outputPropertyNames, out structuredContent);
    }

    static bool TryExtractStructuredContent(string text, out JsonElement structuredContent)
    {
        if (InvocationResultHelpers.TryParseJson(text, out var json) && json.ValueKind == JsonValueKind.Object)
        {
            structuredContent = json;

            return true;
        }

        structuredContent = default;

        return false;
    }

    static bool TryApplyOutputSchema(JsonElement content, JsonNode outputSchema, IReadOnlySet<string>? outputPropertyNames, out JsonElement structuredContent)
    {
        if (outputSchema is not JsonObject root || outputPropertyNames is null)
        {
            structuredContent = content;

            return true;
        }

        if (!ValidateJsonValue(content, root, requireKnownObjectProperties: false))
        {
            structuredContent = default;

            return false;
        }

        var filtered = new JsonObject();

        foreach (var prop in content.EnumerateObject())
        {
            if (outputPropertyNames.Contains(prop.Name))
                filtered[prop.Name] = JsonNode.Parse(prop.Value.GetRawText());
        }

        structuredContent = JsonSerializer.SerializeToElement(filtered);

        return true;
    }

    static bool ValidateJsonValue(JsonElement value, JsonObject schema, bool requireKnownObjectProperties)
    {
        if (!MatchesSchemaType(value, schema["type"]))
            return false;

        if (value.ValueKind == JsonValueKind.Object)
            return ValidateJsonObject(value, schema, requireKnownObjectProperties);

        if (value.ValueKind == JsonValueKind.Array && schema["items"] is JsonObject itemSchema)
        {
            foreach (var item in value.EnumerateArray())
            {
                if (!ValidateJsonValue(item, itemSchema, requireKnownObjectProperties: true))
                    return false;
            }
        }

        return true;
    }

    static bool ValidateJsonObject(JsonElement value, JsonObject schema, bool requireKnownObjectProperties)
    {
        var props = schema["properties"] as JsonObject;

        if (schema["required"] is JsonArray required)
        {
            foreach (var requiredProp in required)
            {
                if (requiredProp?.GetValue<string>() is { } name && !value.TryGetProperty(name, out _))
                    return false;
            }
        }

        if (props is null)
            return true;

        foreach (var prop in value.EnumerateObject())
        {
            if (!props.TryGetPropertyValue(prop.Name, out var propSchema))
            {
                if (requireKnownObjectProperties)
                    return false;

                continue;
            }

            if (propSchema is JsonObject propSchemaObject && !ValidateJsonValue(prop.Value, propSchemaObject, requireKnownObjectProperties: true))
                return false;
        }

        return true;
    }

    static bool MatchesSchemaType(JsonElement value, JsonNode? typeNode)
    {
        return typeNode switch
        {
            null => true,
            JsonValue typeValue when typeValue.TryGetValue<string>(out var type) => MatchesSchemaType(value, type),
            JsonArray types => types.Any(t => t is JsonValue item && item.TryGetValue<string>(out var type) && MatchesSchemaType(value, type)),
            _ => true
        };
    }

    static bool MatchesSchemaType(JsonElement value, string type)
        => type switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "number" => value.ValueKind == JsonValueKind.Number,
            "null" => value.ValueKind == JsonValueKind.Null,
            _ => true
        };
}