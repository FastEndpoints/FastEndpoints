using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class OperationSchemaHelpers
{
    const BindingFlags PublicInstanceHierarchy = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

    internal static OpenApiSchema StringSchema()
        => new() { Type = JsonSchemaType.String };

    extension(IOpenApiSchema? schema)
    {
        internal OpenApiSchema? ResolveSchema()
            => schema switch
            {
                OpenApiSchemaReference schemaRef => schemaRef.Target as OpenApiSchema,
                OpenApiSchema concreteSchema => concreteSchema,
                _ => null
            };

        internal IOpenApiSchema? ResolveSchemaOrReference()
            => schema switch
            {
                OpenApiSchemaReference schemaRef => schemaRef.Target,
                OpenApiSchema concreteSchema => concreteSchema,
                _ => null
            };
    }

    extension(Type type)
    {
        internal IOpenApiSchema GetSchemaForType(bool shortSchemaNames = false)
        {
            var actualType = Nullable.GetUnderlyingType(type) ?? type;

            if (actualType != typeof(string))
            {
                var elementType = GetCollectionElementType(actualType);

                if (elementType is not null)
                {
                    return new OpenApiSchema
                    {
                        Type = JsonSchemaType.Array,
                        Items = elementType.GetSchemaForType(shortSchemaNames)
                    };
                }
            }

            if (TryCreatePrimitiveSchema(actualType) is { } primitiveSchema)
                return primitiveSchema;

            var refId = SchemaNameGenerator.GetReferenceId(actualType, shortSchemaNames);

            if (refId is not null)
                return new OpenApiSchemaReference(refId);

            return StringSchema();
        }

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
                _ when underlying.IsEnum => 0,
                _ => null
            };
        }

        internal JsonNode? GenerateSampleJsonNode(HashSet<Type>? visited = null)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying != typeof(string))
            {
                var elementType = GetCollectionElementType(underlying);

                if (elementType is not null)
                {
                    var itemSample = elementType.GenerateSampleJsonNode(visited);

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
            var policy = Extensions.NamingPolicy;

            foreach (var prop in underlying.GetProperties(PublicInstanceHierarchy))
            {
                if (prop.GetSetMethod() is null)
                    continue;

                var propName = policy?.ConvertName(prop.Name) ?? prop.Name;
                var sample = prop.PropertyType.GetSampleValue(propName);

                if (sample is not null)
                    obj[propName] = sample.JsonNodeFromObject();
                else
                {
                    var nested = prop.PropertyType.GenerateSampleJsonNode(visited);

                    if (nested is not null)
                        obj[propName] = nested;
                }
            }

            visited.Remove(underlying);

            return obj.Count > 0 ? obj : null;
        }
    }

    internal static void RemovePropFromRequestBody(this OpenApiOperation operation, string propName, HashSet<string>? removedProps = null)
    {
        if (operation.RequestBody?.Content is null)
            return;

        var policy = Extensions.SelectedJsonNamingPolicy;
        var schemaName = policy?.ConvertName(propName) ?? propName;

        removedProps?.Add(schemaName);

        foreach (var content in operation.RequestBody.Content.Values)
        {
            if (content.Schema?.Properties is null)
                continue;

            var key = content.Schema.Properties.Keys.FindCaseInsensitiveKey(schemaName);

            if (key is not null)
            {
                content.Schema.Properties.Remove(key);
                content.Schema.Required?.Remove(key);
            }
        }
    }

    internal static Type? TryResolveRouteConstraintType(this string rawSegment)
    {
        var colonIdx = rawSegment.IndexOf(':');

        if (colonIdx < 0 || colonIdx == rawSegment.Length - 1)
            return null;

        var tail = rawSegment[(colonIdx + 1)..];
        var constraintName = tail;

        var parenIdx = constraintName.IndexOf('(');
        if (parenIdx >= 0)
            constraintName = constraintName[..parenIdx];

        var nextColonIdx = constraintName.IndexOf(':');
        if (nextColonIdx >= 0)
            constraintName = constraintName[..nextColonIdx];

        constraintName = constraintName.TrimEnd('?');

        return GlobalConfig.RouteConstraintMap.GetValueOrDefault(constraintName);
    }

    internal static bool IsRequestBodyEmpty(this OpenApiOperation operation)
    {
        if (operation.RequestBody?.Content is null)
            return true;

        return operation.RequestBody.Content.Values.All(c => IsContentSchemaEmpty(c.Schema));

        static bool IsContentSchemaEmpty(IOpenApiSchema? schema)
        {
            switch (schema)
            {
                case null:
                    return true;
                case OpenApiSchemaReference r:
                {
                    var target = r.Target;

                    return target is null || (target.Properties is null or { Count: 0 } && target.Type != JsonSchemaType.Array);
                }
                case OpenApiSchema s:
                    return s.Type != JsonSchemaType.Array &&
                           s.Type != JsonSchemaType.String &&
                           s.Type != JsonSchemaType.Integer &&
                           s.Type != JsonSchemaType.Number &&
                           s.Type != JsonSchemaType.Boolean &&
                           (s.Properties is null || s.Properties.Count == 0) &&
                           s.OneOf is null or { Count: 0 } &&
                           s.AnyOf is null or { Count: 0 } &&
                           s.AllOf is null or { Count: 0 };
                default:
                    return true;
            }
        }
    }

    extension(object? value)
    {
        internal JsonNode? JsonNodeFromObject()
        {
            if (value is null)
                return null;

            try
            {
                return JsonSerializer.SerializeToNode(value, Cfg.SerOpts.Options);
            }
            catch
            {
                return null;
            }
        }

        internal JsonObject? JsonObjectFromObject(Type? valueType = null)
        {
            if (value is null)
                return null;

            try
            {
                return valueType is null
                           ? JsonSerializer.SerializeToNode(value, Cfg.SerOpts.Options) as JsonObject
                           : JsonNode.Parse(JsonSerializer.Serialize(value, valueType, Cfg.SerOpts.Options)) as
                                 JsonObject;
            }
            catch
            {
                return null;
            }
        }
    }

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

    static OpenApiSchema? TryCreatePrimitiveSchema(Type type)
        => type switch
        {
            _ when type == typeof(string) => new() { Type = JsonSchemaType.String },
            _ when type == typeof(int) || type == typeof(short) => new() { Type = JsonSchemaType.Integer, Format = "int32" },
            _ when type == typeof(long) => new() { Type = JsonSchemaType.Integer, Format = "int64" },
            _ when type == typeof(float) => new() { Type = JsonSchemaType.Number, Format = "float" },
            _ when type == typeof(double) => new() { Type = JsonSchemaType.Number, Format = "double" },
            _ when type == typeof(decimal) => new() { Type = JsonSchemaType.Number, Format = "decimal" },
            _ when type == typeof(bool) => new() { Type = JsonSchemaType.Boolean },
            _ when type == typeof(Guid) => new() { Type = JsonSchemaType.String, Format = "uuid" },
            _ when type == typeof(DateTime) || type == typeof(DateTimeOffset) => new() { Type = JsonSchemaType.String, Format = "date-time" },
            _ when type == typeof(DateOnly) => new() { Type = JsonSchemaType.String, Format = "date" },
            _ when type == typeof(TimeOnly) => new() { Type = JsonSchemaType.String, Format = "time" },
            _ => null
        };

    static Type? GetCollectionElementType(Type type)
    {
        if (type.IsArray)
            return type.GetElementType();

        if (type.IsGenericType)
        {
            var genDef = type.GetGenericTypeDefinition();

            if (genDef == typeof(IEnumerable<>) ||
                genDef == typeof(ICollection<>) ||
                genDef == typeof(IList<>) ||
                genDef == typeof(List<>) ||
                genDef == typeof(IReadOnlyList<>) ||
                genDef == typeof(IReadOnlyCollection<>) ||
                genDef == typeof(HashSet<>) ||
                genDef == typeof(ISet<>))
                return type.GetGenericArguments()[0];
        }

        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return iface.GetGenericArguments()[0];
        }

        return null;
    }
}