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
}
