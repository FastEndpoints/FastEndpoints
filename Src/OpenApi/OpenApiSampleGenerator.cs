using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FastEndpoints.OpenApi;

static partial class OperationSchemaHelpers
{
    const BindingFlags PublicInstanceHierarchy = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
    static readonly ConcurrentDictionary<Type, PropertyInfo[]> _sampleJsonPropertyCache = new();

    extension(Type type)
    {
        internal object? GetSampleValue(string? propertyName = null)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            return underlying switch
            {
                _ when underlying == typeof(int) || underlying == typeof(short) => 0,
                _ when underlying == typeof(long) => 0L,
                _ when underlying == typeof(float) => 0f,
                _ when underlying == typeof(double) => 0d,
                _ when underlying == typeof(decimal) => 0m,
                _ when underlying == typeof(bool) => false,
                _ when underlying == typeof(string) => propertyName ?? "",
                _ when underlying == typeof(Guid) => Guid.Empty,
                _ when underlying == typeof(DateTime) => DateTime.MinValue,
                _ when underlying == typeof(DateTimeOffset) => DateTimeOffset.MinValue,
                _ when underlying == typeof(DateOnly) => new DateOnly(2020, 10, 10),
                _ when underlying == typeof(TimeOnly) => new TimeOnly(0, 0, 0),
                _ when underlying.IsEnum => Enum.GetValues(underlying).GetValue(0),
                _ => null
            };
        }

        internal JsonNode? GenerateSampleJsonNode(JsonNamingPolicy? namingPolicy = null,
                                                 bool usePropertyNamingPolicy = true,
                                                 HashSet<Type>? visited = null)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying != typeof(string))
            {
                if (TryGetDictionaryValueType(underlying) is { } dictionaryValueType)
                {
                    var valueSample = dictionaryValueType.GenerateSampleJsonNode(namingPolicy, usePropertyNamingPolicy, visited) ??
                                      dictionaryValueType.GetSampleValue("additionalProp1")?.JsonNodeFromObject();

                    return valueSample is not null
                               ? new JsonObject { ["additionalProp1"] = valueSample }
                               : new JsonObject();
                }

                var elementType = TryGetCollectionElementType(underlying);

                if (elementType is not null)
                {
                    var itemSample = elementType.GenerateSampleJsonNode(namingPolicy, usePropertyNamingPolicy, visited);

                    if (itemSample is not null)
                        return new JsonArray(itemSample);

                    var elemSample = elementType.GetSampleValue();

                    if (elemSample is not null)
                        return new JsonArray(elemSample.JsonNodeFromObject());

                    return null;
                }
            }

            if (underlying.IsPrimitive ||
                underlying.IsEnum ||
                underlying == typeof(string) ||
                underlying == typeof(decimal) ||
                underlying == typeof(Guid) ||
                underlying == typeof(DateTime) ||
                underlying == typeof(DateTimeOffset) ||
                underlying == typeof(DateOnly) ||
                underlying == typeof(TimeOnly))
                return null;

            visited ??= [];

            if (!visited.Add(underlying))
                return null;

            var obj = new JsonObject();

            foreach (var prop in GetSampleJsonProperties(underlying))
            {
                var propName = PropertyNameResolver.GetSchemaPropertyName(prop, namingPolicy, usePropertyNamingPolicy);
                var sample = prop.PropertyType.GetSampleValue(propName);

                if (sample is not null)
                    obj[propName] = sample.JsonNodeFromObject();
                else
                {
                    var nested = prop.PropertyType.GenerateSampleJsonNode(namingPolicy, usePropertyNamingPolicy, visited);

                    if (nested is not null)
                        obj[propName] = nested;
                }
            }

            visited.Remove(underlying);

            return obj.Count > 0 ? obj : null;
        }
    }

    static PropertyInfo[] GetSampleJsonProperties(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return _sampleJsonPropertyCache.GetOrAdd(
            type,
            static t => t.GetProperties(PublicInstanceHierarchy)
                         .Where(p => p.GetSetMethod() is not null)
                         .ToArray());
    }
}
