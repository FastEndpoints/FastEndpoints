using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static partial class OperationSchemaHelpers
{
    static readonly ConcurrentDictionary<Type, TypeLookupResult> _collectionElementTypeCache = new();
    static readonly ConcurrentDictionary<Type, TypeLookupResult> _dictionaryValueTypeCache = new();

    readonly record struct TypeLookupResult(Type? Type);

    internal static OpenApiSchema StringSchema()
        => new() { Type = JsonSchemaType.String };

    internal static void RemoveProperties(this JsonObject obj, IEnumerable<string> propertyNames)
    {
        var existingKeys = obj.Select(static kvp => KeyValuePair.Create(kvp.Key, kvp.Key))
                              .ToCaseInsensitiveDictionary(obj.Count);

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

    internal static Dictionary<string, TValue> ToCaseInsensitiveDictionary<TValue>(this IEnumerable<KeyValuePair<string, TValue>> values, int capacity = 0)
    {
        var lookup = capacity > 0
                         ? new Dictionary<string, TValue>(capacity, StringComparer.OrdinalIgnoreCase)
                         : new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in values)
            lookup.TryAdd(key, value);

        return lookup;
    }

    internal static void SortByKey<TValue>(this IDictionary<string, TValue> dictionary)
    {
        if (dictionary.Count < 2)
            return;

        var sorted = dictionary.OrderBy(static kvp => kvp.Key, StringComparer.Ordinal).ToList();
        dictionary.Clear();

        foreach (var (key, value) in sorted)
            dictionary[key] = value;
    }

    internal static IOrderedEnumerable<KeyValuePair<string, TValue>> OrderByKey<TValue>(this IEnumerable<KeyValuePair<string, TValue>> values)
        => values.OrderBy(static kvp => kvp.Key, StringComparer.Ordinal);

    internal static void AddHeader(this OpenApiResponse response, string headerName, OpenApiHeader header)
    {
        response.Headers ??= new Dictionary<string, IOpenApiHeader>();
        response.Headers[headerName] = header;
    }

    internal static Type GetOpenApiParameterType(this Type type)
        => type.Name.EndsWith("HeaderValue", StringComparison.Ordinal) ? typeof(string) : type;

    internal static Type? TryGetCollectionElementType(Type type)
    {
        type = type.GetUnderlyingType();

        return _collectionElementTypeCache.GetOrAdd(type, static t => new(ResolveCollectionElementType(t))).Type;
    }

    static Type? ResolveCollectionElementType(Type type)
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
        => value.TryGetValue<bool>(out _) ? new() { Type = JsonSchemaType.Boolean } :
           value.TryGetValue<int>(out _) ? new() { Type = JsonSchemaType.Integer, Format = "int32" } :
           value.TryGetValue<long>(out _) ? new() { Type = JsonSchemaType.Integer, Format = "int64" } :
           value.TryGetValue<decimal>(out _) ? new() { Type = JsonSchemaType.Number } :
           StringSchema();

    static readonly HashSet<string> _setLikeGenericTypes =
    [
        "System.Collections.Generic.ISet`1",
        "System.Collections.Generic.IReadOnlySet`1",
        "System.Collections.Generic.HashSet`1",
        "System.Collections.Generic.SortedSet`1",
        "System.Collections.Frozen.FrozenSet`1",
        "System.Collections.Immutable.IImmutableSet`1",
        "System.Collections.Immutable.ImmutableHashSet`1",
        "System.Collections.Immutable.ImmutableSortedSet`1"
    ];

    internal static void ApplyUniqueItems(OpenApiSchema schema, Type collectionType, MemberInfo? member = null)
    {
        if (!IsUniqueItemsCollection(collectionType, member))
            return;

        schema.UniqueItems = true;
    }

    static bool IsUniqueItemsCollection(Type type, MemberInfo? member)
    {
        type = type.GetUnderlyingType();

        if (type == typeof(string) || type == typeof(byte[]))
            return false;

        var elementType = TryGetCollectionElementType(type);

        if (elementType is null)
            return false;

        if (member?.IsDefined(typeof(UniqueItemsAttribute), true) is true)
            return true;

        return IsSetLikeCollectionType(type) && !elementType.GetUnderlyingType().IsComplexType();
    }

    static bool IsSetLikeCollectionType(Type type)
    {
        if (MatchesSetLikeType(type))
            return true;

        foreach (var interfaceType in type.GetInterfaces())
        {
            if (MatchesSetLikeType(interfaceType))
                return true;
        }

        return false;

        static bool MatchesSetLikeType(Type candidate)
        {
            if (!candidate.IsGenericType)
                return false;

            var genericDef = candidate.IsGenericTypeDefinition ? candidate : candidate.GetGenericTypeDefinition();
            var genericName = genericDef.FullName;

            return genericName is not null && _setLikeGenericTypes.Contains(genericName);
        }
    }
}
