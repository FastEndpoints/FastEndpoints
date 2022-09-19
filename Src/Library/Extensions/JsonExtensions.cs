using Microsoft.Extensions.Primitives;
using System.Text.Json.Nodes;

namespace FastEndpoints;

public static class JsonExtensions
{
    public static JsonArray SetValues(this JsonArray array, StringValues values)
    {
        for (var i = 0; i < values.Count; i++)
            array.Add((string?)values[i]);
        return array;
    }

    public static void SetNestedValues(this JsonObject obj, string[] keys, StringValues values)
    {
        JsonNode node = obj;

        for (var i = 0; i < keys.Length - 1; i++)
        {
            var key = keys[i];
            node = node[key] ??= new JsonObject();
        }

        node[keys[^1]] =
            values.Count > 1
            ? new JsonArray().SetValues(values)
            : values[0];
    }
}
