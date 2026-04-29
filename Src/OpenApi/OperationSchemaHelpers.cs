using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static partial class OperationSchemaHelpers
{
    internal static OpenApiSchema StringSchema()
        => new() { Type = JsonSchemaType.String };

    internal static void RemoveProperties(this JsonObject obj, IEnumerable<string> propertyNames)
    {
        var existingKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in obj.Select(static kvp => kvp.Key))
            existingKeys.TryAdd(key, key);

        foreach (var propertyName in propertyNames)
        {
            if (!existingKeys.Remove(propertyName, out var existingKey))
                continue;

            obj.Remove(existingKey);
        }
    }

    internal static string? FindCaseInsensitiveKey(this IEnumerable<string> keys, string match)
    {
        foreach (var key in keys)
        {
            if (string.Equals(key, match, StringComparison.OrdinalIgnoreCase))
                return key;
        }

        return null;
    }

    internal static Type? TryGetCollectionElementType(Type type)
    {
        type = type.GetUnderlyingType();

        if (type == typeof(string))
            return null;

        if (TryGetDictionaryValueType(type) is not null)
            return null;

        if (type.IsArray)
            return type.GetElementType();

        if (type.IsGenericType && TryMatchEnumerable(type) is { } directMatch)
            return directMatch;

        foreach (var interfaceType in type.GetInterfaces())
        {
            if (TryMatchEnumerable(interfaceType) is { } interfaceMatch)
                return interfaceMatch;
        }

        return null;

        static Type? TryMatchEnumerable(Type candidate)
        {
            if (!candidate.IsGenericType)
                return null;

            var genericType = candidate.GetGenericTypeDefinition();

            return genericType == typeof(IEnumerable<>)
                       ? candidate.GetGenericArguments()[0]
                       : null;
        }
    }

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
