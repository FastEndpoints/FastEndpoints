using System.Collections.Concurrent;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static partial class OperationSchemaHelpers
{
    extension(Type type)
    {
        internal IOpenApiSchema GetSchemaForType(bool shortSchemaNames = false)
        {
            var actualType = Nullable.GetUnderlyingType(type) ?? type;

            if (TryGetDictionaryValueType(actualType) is { } dictionaryValueType)
            {
                return new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    AdditionalProperties = dictionaryValueType.GetSchemaForType(shortSchemaNames)
                };
            }

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
    }

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

    internal static Type? TryGetDictionaryValueType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (TryMatchDictionary(type) is { } directMatch)
            return directMatch;

        foreach (var iface in type.GetInterfaces())
        {
            if (TryMatchDictionary(iface) is { } interfaceMatch)
                return interfaceMatch;
        }

        return null;

        static Type? TryMatchDictionary(Type candidate)
        {
            if (!candidate.IsGenericType)
                return null;

            var genDef = candidate.GetGenericTypeDefinition();

            if (genDef != typeof(Dictionary<,>) &&
                genDef != typeof(IDictionary<,>) &&
                genDef != typeof(IReadOnlyDictionary<,>) &&
                genDef != typeof(ConcurrentDictionary<,>))
                return null;

            var args = candidate.GetGenericArguments();

            return args[0] == typeof(string) ? args[1] : null;
        }
    }
}
