using Microsoft.Extensions.Primitives;
using System.Text.Json.Nodes;

namespace FastEndpoints.Extensions;
public static class JsonExtensions
{

    public static JsonArray SetValues(this JsonArray array, StringValues values)
    {
        foreach (var value in values)
        {
            array.Add(value);
        }
        return array;
    }
    public static void SetNestedValues(this JsonObject obj, string[] keys, StringValues values)
    {
        JsonNode node = obj;
        for (var i = 0; i < keys.Length - 1; i++)
        {
            var key = keys[i];
            if (node[key] is null)
            {
                node[key] ??= new JsonObject();
            }
            node = node[key]!;
        }
        node[keys[^1]] = values.Count > 1 ? new JsonArray().SetValues(values) : values[0];
    }
}
