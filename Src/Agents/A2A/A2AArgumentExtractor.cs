using System.Text.Json;

namespace FastEndpoints.A2A;

static class A2AArgumentExtractor
{
    public static JsonElement Extract(A2AMessage message)
    {
        foreach (var part in message.Parts!)
        {
            if (part.Data is { ValueKind: not JsonValueKind.Undefined } data)
                return data;

            if (part.Text is { } text)
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);

                    return doc.RootElement.Clone();
                }
                catch (JsonException)
                {
                    throw new A2ARpcException(JsonRpcError.InvalidParams("text parts must contain valid JSON to invoke a skill."));
                }
            }
        }

        throw new A2ARpcException(JsonRpcError.InvalidParams("no supported input part found. only 'data' and JSON 'text' parts can invoke skills."));
    }

    public static string? GetRequestedSkill(JsonElement? metadata)
    {
        if (metadata is null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
            return null;

        if (metadata.Value.ValueKind != JsonValueKind.Object || !metadata.Value.TryGetProperty("skill", out var skill))
            return null;

        if (skill.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(skill.GetString()))
            throw new A2ARpcException(JsonRpcError.InvalidParams("'metadata.skill' must be a non-empty string."));

        return skill.GetString();
    }
}
