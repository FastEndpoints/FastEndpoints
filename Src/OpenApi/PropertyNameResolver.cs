using System.Reflection;
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

            var property = currentType.GetUnderlyingType().GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            if (property is null)
            {
                segments[i] = usePropertyNamingPolicy ? namingPolicy?.ConvertName(segment) ?? segment : segment;
                currentType = typeof(object);
                continue;
            }

            segments[i] = GetSchemaPropertyName(property, namingPolicy, usePropertyNamingPolicy);
            currentType = property.PropertyType;
        }

        return string.Join(".", segments);
    }
}
