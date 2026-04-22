using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FastEndpoints.OpenApi;

static class PropertyNameResolver
{
    internal static string GetSchemaPropertyName(PropertyInfo property, JsonNamingPolicy? namingPolicy = null)
        => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ??
           (namingPolicy ?? Extensions.NamingPolicy)?.ConvertName(property.Name) ??
           property.Name;

    internal static string ConvertPropertyPath(Type declaringType, string propertyPath, JsonNamingPolicy? namingPolicy = null)
    {
        if (string.IsNullOrWhiteSpace(propertyPath))
            return propertyPath;

        namingPolicy ??= Extensions.NamingPolicy;
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
                segments[i] = namingPolicy?.ConvertName(segment) ?? segment;
                currentType = typeof(object);
                continue;
            }

            segments[i] = GetSchemaPropertyName(property, namingPolicy);
            currentType = property.PropertyType;
        }

        return string.Join(".", segments);
    }
}
