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

            if (actualType == typeof(byte[]))
                return ByteArraySchema();

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
                var elementType = TryGetCollectionElementType(actualType);

                if (elementType is not null)
                {
                    var schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Array,
                        Items = elementType.GetSchemaForType(shortSchemaNames)
                    };

                    ApplyUniqueItems(schema, actualType);

                    return schema;
                }
            }

            if (TryCreatePrimitiveSchema(actualType) is { } primitiveSchema)
                return primitiveSchema;

            var refId = SchemaNameGenerator.GetReferenceId(actualType, shortSchemaNames);

            if (refId is not null)
                return new OpenApiSchemaReference(refId);

            return StringSchema();
        }

        internal IOpenApiSchema GetSchemaForType(SharedContext sharedCtx, bool shortSchemaNames = false)
        {
            var schema = type.GetSchemaForType(shortSchemaNames);

            RegisterMissingSchemaTypes(type, schema, sharedCtx);

            return schema;
        }
    }

    static void RegisterMissingSchemaTypes(Type type, IOpenApiSchema schema, SharedContext sharedCtx)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (schema is OpenApiSchemaReference schemaRef)
        {
            if (GetReferenceId(schemaRef) is { } refId)
                sharedCtx.MissingSchemaTypes.TryAdd(refId, type);

            return;
        }

        if (schema is not OpenApiSchema concreteSchema)
            return;

        if (TryGetDictionaryValueType(type) is { } dictionaryValueType && concreteSchema.AdditionalProperties is { } additionalProperties)
        {
            RegisterMissingSchemaTypes(dictionaryValueType, additionalProperties, sharedCtx);

            return;
        }

        if (type != typeof(string) && type != typeof(byte[]) && TryGetCollectionElementType(type) is { } elementType && concreteSchema.Items is { } items)
            RegisterMissingSchemaTypes(elementType, items, sharedCtx);
    }

    static OpenApiSchema ByteArraySchema()
        => new() { Type = JsonSchemaType.String, Format = "byte" };

    static OpenApiSchema? TryCreatePrimitiveSchema(Type type)
        => type switch
        {
            _ when type == typeof(string) => new() { Type = JsonSchemaType.String },
            _ when type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) || type == typeof(int) || type == typeof(uint)
                => new() { Type = JsonSchemaType.Integer, Format = "int32" },
            _ when type == typeof(long) || type == typeof(ulong) => new() { Type = JsonSchemaType.Integer, Format = "int64" },
            _ when type == typeof(float) => new() { Type = JsonSchemaType.Number, Format = "float" },
            _ when type == typeof(double) => new() { Type = JsonSchemaType.Number, Format = "double" },
            _ when type == typeof(decimal) => new() { Type = JsonSchemaType.Number, Format = "decimal" },
            _ when type == typeof(bool) => new() { Type = JsonSchemaType.Boolean },
            _ when type == typeof(char) => new() { Type = JsonSchemaType.String, MinLength = 1, MaxLength = 1 },
            _ when type == typeof(Guid) => new() { Type = JsonSchemaType.String, Format = "uuid" },
            _ when type == typeof(DateTime) || type == typeof(DateTimeOffset) => new() { Type = JsonSchemaType.String, Format = "date-time" },
            _ when type == typeof(DateOnly) => new() { Type = JsonSchemaType.String, Format = "date" },
            _ when type == typeof(TimeOnly) => new() { Type = JsonSchemaType.String, Format = "time" },
            _ => null
        };

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