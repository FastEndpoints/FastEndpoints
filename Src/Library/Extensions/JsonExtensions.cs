using Microsoft.Extensions.Primitives;
using System.Text.Json.Nodes;

namespace FastEndpoints;

internal static class JsonExtensions
{
    internal static JsonArray SetValues(this JsonArray array, StringValues values)
    {
        for (var i = 0; i < values.Count; i++)
            array.Add((string?)values[i]);
        return array;
    }

    internal static JsonNode GetOrCreateLastNode(this JsonNode node, string[] keys)
    {
        for (var i = 0; i < keys.Length - 1; i++)
        {
            var key = keys[i];
            node = node[key] ??= new JsonObject();
        }
        return node;
    }
}
