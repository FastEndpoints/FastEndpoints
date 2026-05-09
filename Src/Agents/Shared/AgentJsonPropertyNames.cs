using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace FastEndpoints.Agents;

static class AgentJsonPropertyNames
{
    static readonly ConcurrentDictionary<(Type DtoType, JsonSerializerOptions SerializerOptions), IReadOnlyDictionary<string, string>> _schemaNameMaps = new();

    internal static string? GetSerializedName(PropertyInfo prop, EndpointDefinition def, JsonSerializerOptions serializerOptions)
        => TryGetJsonPropertyName(prop, def.ReqDtoType, def.SerializerContext ?? serializerOptions.TypeInfoResolver, serializerOptions);

    internal static string? GetSerializedName(PropertyInfo prop, Type dtoType, JsonSerializerOptions serializerOptions)
        => BuildSchemaNameMap(dtoType, serializerOptions).GetValueOrDefault(prop.Name);

    internal static IEnumerable<string> GetSchemaNameCandidates(PropertyInfo prop, Type dtoType, JsonSerializerOptions serializerOptions)
    {
        if (GetSerializedName(prop, dtoType, serializerOptions) is { } jsonName)
            yield return jsonName;

        yield return prop.Name;
        yield return prop.FieldName();
    }

    internal static IReadOnlyDictionary<string, string> BuildSchemaNameMap(Type dtoType, JsonSerializerOptions serializerOptions)
        => _schemaNameMaps.GetOrAdd((dtoType, serializerOptions), static key => BuildSchemaNameMapCore(key.DtoType, key.SerializerOptions));

    static Dictionary<string, string> BuildSchemaNameMapCore(Type dtoType, JsonSerializerOptions serializerOptions)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var typeInfo = serializerOptions.TypeInfoResolver?.GetTypeInfo(dtoType, serializerOptions);

        if (typeInfo is not null)
        {
            foreach (var jsonProp in typeInfo.Properties)
            {
                if (jsonProp.AttributeProvider is PropertyInfo prop)
                    map[prop.Name] = jsonProp.Name;
            }
        }

        foreach (var prop in dtoType.BindableProps())
            map.TryAdd(prop.Name, prop.FieldName());

        return map;
    }

    static string? TryGetJsonPropertyName(PropertyInfo prop, Type dtoType, IJsonTypeInfoResolver? typeInfoResolver, JsonSerializerOptions serializerOptions)
    {
        var typeInfo = typeInfoResolver?.GetTypeInfo(dtoType, serializerOptions);

        if (typeInfo is null)
            return null;

        foreach (var jsonProp in typeInfo.Properties)
        {
            if (ReferenceEquals(jsonProp.AttributeProvider, prop))
                return jsonProp.Name;
        }

        return null;
    }
}