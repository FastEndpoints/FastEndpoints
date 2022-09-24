using System.Text.Json.Nodes;

namespace FastEndpoints;

internal static class JsonExtensions
{
    internal static JsonArray SetValues(this JsonArray array, IEnumerable<JsonNode?> values)
    {
        foreach (var value in values)
        {
            array.Add(value);
        }
        return array;
    }

    internal static JsonNode GetOrCreateLastNode(this JsonNode node, string[] keys)
    {
        for (var i = 0; i < keys.Length - 1; i++)
        {
            node = node[keys[i]] ??= new JsonObject();
        }
        return node;
    }
}