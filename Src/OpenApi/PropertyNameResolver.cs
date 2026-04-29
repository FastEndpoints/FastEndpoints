using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FastEndpoints.OpenApi;

static class PropertyNameResolver
{
    internal static string GetSchemaPropertyName(PropertyInfo property, JsonNamingPolicy? namingPolicy = null, bool usePropertyNamingPolicy = true)
        => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
           (usePropertyNamingPolicy ? namingPolicy?.ConvertName(property.Name) : null) ??
           property.Name;

    internal static string ConvertPropertyPath(Type declaringType,
                                               string propertyPath,
                                               JsonNamingPolicy? namingPolicy = null,
                                               bool usePropertyNamingPolicy = true)
    {
        if (string.IsNullOrWhiteSpace(propertyPath))
            return propertyPath;

        var segments = propertyPath.Split('.');
        var currentType = declaringType;

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];

            if (string.IsNullOrWhiteSpace(segment))
                continue;

            var memberName = GetIndexerStart(segment) is { } indexerStart
                                 ? segment[..indexerStart]
                                 : segment;
            var indexers = NormalizeIndexers(segment[memberName.Length..]);
            var property = ResolveProperty(currentType, memberName);

            if (property is null)
            {
                var convertedSegment = string.IsNullOrEmpty(memberName)
                                           ? string.Empty
                                           : usePropertyNamingPolicy
                                               ? namingPolicy?.ConvertName(memberName) ?? memberName
                                               : memberName;

                segments[i] = convertedSegment + indexers;
                currentType = typeof(object);
                continue;
            }

            segments[i] = GetSchemaPropertyName(property, namingPolicy, usePropertyNamingPolicy) + indexers;
            currentType = property.PropertyType;

            if (indexers.Length > 0 && TryGetCollectionElementType(currentType) is { } elementType)
                currentType = elementType;
        }

        return string.Join(".", segments);
    }

    static PropertyInfo? ResolveProperty(Type type, string memberName)
    {
        if (string.IsNullOrEmpty(memberName))
            return null;

        var currentType = type.GetUnderlyingType();
        var property = currentType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        if (property is not null)
            return property;

        return TryGetCollectionElementType(currentType) is { } elementType
                   ? elementType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                   : null;
    }

    static int? GetIndexerStart(string segment)
    {
        var index = segment.IndexOf('[');

        return index < 0 ? null : index;
    }

    static string NormalizeIndexers(string value)
    {
        if (value.Length == 0)
            return string.Empty;

        StringBuilder? result = null;
        var index = 0;

        while (index < value.Length)
        {
            var start = value.IndexOf('[', index);

            if (start < 0)
                break;

            var end = value.IndexOf(']', start + 1);

            if (end < 0)
                break;

            result ??= new();
            result.Append("[]");
            index = end + 1;
        }

        return result?.ToString() ?? string.Empty;
    }

    static Type? TryGetCollectionElementType(Type type)
    {
        type = type.GetUnderlyingType();

        if (type == typeof(string))
            return null;

        if (type.IsArray)
            return type.GetElementType();

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return type.GetGenericArguments()[0];

        foreach (var interfaceType in type.GetInterfaces())
        {
            if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return interfaceType.GetGenericArguments()[0];
        }

        return null;
    }
}
