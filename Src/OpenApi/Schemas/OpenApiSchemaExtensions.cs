using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static partial class OperationSchemaHelpers
{
    static readonly ConstructorInfo? _openApiSchemaCopyCtor = typeof(OpenApiSchema).GetConstructor([typeof(IOpenApiSchema)]);
    static readonly PropertyInfo[] _openApiSchemaProperties = typeof(OpenApiSchema).GetProperties(BindingFlags.Public | BindingFlags.Instance);
    static readonly ConcurrentDictionary<Type, PropertyInfo[]> _cloneablePropertiesCache = new();

    extension(IOpenApiSchema? schema)
    {
        internal OpenApiSchema? ResolveSchema()
            => schema switch
            {
                OpenApiSchemaReference schemaRef => ResolveSchemaReference(schemaRef) as OpenApiSchema,
                OpenApiSchema concreteSchema => concreteSchema,
                _ => null
            };

        internal OpenApiSchema? ResolveSchema(SharedContext sharedCtx)
            => schema switch
            {
                OpenApiSchemaReference schemaRef when schemaRef.GetReferenceId() is { } refId && sharedCtx.TryGetOperationSchemaVariant(refId, out var variant) => variant,
                _ => schema.ResolveSchema()
            };

        internal IOpenApiSchema? ResolveSchemaOrReference()
            => schema switch
            {
                OpenApiSchemaReference schemaRef => ResolveSchemaReference(schemaRef),
                OpenApiSchema concreteSchema => concreteSchema,
                _ => null
            };

        internal IOpenApiSchema? ResolveSchemaOrReference(SharedContext sharedCtx)
            => schema switch
            {
                OpenApiSchemaReference schemaRef when schemaRef.GetReferenceId() is { } refId && sharedCtx.TryGetOperationSchemaVariant(refId, out var variant) => variant,
                _ => schema.ResolveSchemaOrReference()
            };

        internal OpenApiSchema? CloneAsConcreteSchema()
            => schema switch
            {
                OpenApiSchema concreteSchema => CloneConcreteSchema(concreteSchema),
                OpenApiSchemaReference schemaRef when ResolveSchemaReference(schemaRef) is { } target => CloneConcreteSchema(target),
                _ => null
            };
    }

    extension(OpenApiMediaType mediaType)
    {
        internal OpenApiSchema? EnsureOperationLocalSchema()
        {
            var cloned = mediaType.Schema.CloneAsConcreteSchema();

            if (cloned is not null)
                mediaType.Schema = cloned;

            return cloned;
        }

        internal OpenApiSchema? EnsureOperationLocalSchemaForMutation()
            => mediaType.EnsureOperationLocalSchema();

        internal OpenApiSchema? EnsureOperationLocalSchemaForMutation(SharedContext sharedCtx, string operationKey, string schemaKey)
            => mediaType.Schema.EnsureSchemaForMutation(
                sharedCtx,
                operationKey,
                schemaKey,
                localized => mediaType.Schema = localized,
                cloneConcreteSchema: true);

        internal OpenApiSchema? EnsureOperationLocalSchemaForMutation(OperationSchemaMutationContext mutationCtx, string schemaKey)
            => mediaType.Schema.EnsureSchemaForMutation(
                mutationCtx,
                schemaKey,
                localized => mediaType.Schema = localized,
                cloneConcreteSchema: true);
    }

    extension(IOpenApiSchema? schema)
    {
        internal OpenApiSchema? EnsureSchemaForMutation(OperationSchemaMutationContext mutationCtx,
                                                        string schemaKey,
                                                        Action<IOpenApiSchema> replace,
                                                        bool cloneConcreteSchema = false)
            => schema.EnsureSchemaForMutation(
                mutationCtx.SharedContext,
                mutationCtx.OperationKey,
                schemaKey,
                replace,
                cloneConcreteSchema);

        internal OpenApiSchema? EnsureSchemaForMutation(SharedContext sharedCtx,
                                                        string operationKey,
                                                        string schemaKey,
                                                        Action<IOpenApiSchema> replace,
                                                        bool cloneConcreteSchema = false)
        {
            switch (schema)
            {
                case OpenApiSchemaReference schemaRef when schemaRef.GetReferenceId() is { } refId:
                {
                    if (sharedCtx.TryGetOperationSchemaVariant(refId, out var existingVariant))
                        return existingVariant;

                    var cloned = schemaRef.CloneAsConcreteSchema();

                    if (cloned is null)
                        return null;

                    var variant = sharedCtx.GetOrAddOperationSchemaVariant(refId, operationKey, schemaKey, cloned);
                    replace(new OpenApiSchemaReference(variant.RefId));

                    return variant.Schema;
                }
                case OpenApiSchema concreteSchema when cloneConcreteSchema:
                {
                    var cloned = CloneConcreteSchema(concreteSchema);
                    replace(cloned);

                    return cloned;
                }
                case OpenApiSchema concreteSchema:
                    return concreteSchema;
                default:
                    return null;
            }
        }
    }

    static OpenApiSchema CloneConcreteSchema(IOpenApiSchema schema)
    {
        if (_openApiSchemaCopyCtor?.Invoke([schema]) is OpenApiSchema cloned)
            return cloned;

        return CloneConcreteSchemaFallback(schema.ResolveSchema() ?? schema as OpenApiSchema ?? StringSchema());
    }

    static OpenApiSchema CloneConcreteSchemaFallback(OpenApiSchema schema)
    {
        var clone = new OpenApiSchema();

        foreach (var property in _openApiSchemaProperties)
        {
            if (!property.CanRead || !property.CanWrite)
                continue;

            property.SetValue(clone, CloneSchemaMemberValue(property.Name, property.GetValue(schema)));
        }

        return clone;
    }

    static object? CloneSchemaMemberValue(string memberName, object? value)
    {
        if (value is null)
            return null;

        return value switch
        {
            JsonNode node => node.DeepClone(),
            OpenApiSchema concreteSchema => CloneConcreteSchemaFallback(concreteSchema),
            OpenApiSchemaReference schemaRef => schemaRef,
            IOpenApiSchema schema => CloneNestedSchema(schema),
            IDictionary<string, IOpenApiSchema> schemaDictionary => CloneSchemaDictionary(schemaDictionary),
            IDictionary<string, string> stringDictionary => new Dictionary<string, string>(stringDictionary, StringComparer.Ordinal),
            IDictionary<string, bool> boolDictionary => new Dictionary<string, bool>(boolDictionary, StringComparer.Ordinal),
            IDictionary<string, JsonNode> jsonNodeDictionary => CloneJsonNodeDictionary(jsonNodeDictionary),
            IDictionary<string, HashSet<string>> stringSetDictionary => CloneStringSetDictionary(stringSetDictionary),
            IDictionary<string, IOpenApiExtension> extensionsDictionary => CloneExtensionsDictionary(extensionsDictionary),
            IDictionary<string, object> metadataDictionary => CloneMetadataDictionary(memberName, metadataDictionary),
            IList<IOpenApiSchema> schemaList => CloneSchemaList(schemaList),
            ISet<string> requiredSet => new HashSet<string>(requiredSet),
            IList<JsonNode> jsonNodes => CloneJsonNodeList(jsonNodes),
            OpenApiDiscriminator discriminator => CloneOpenApiObject(discriminator),
            OpenApiExternalDocs externalDocs => CloneOpenApiObject(externalDocs),
            OpenApiXml xml => CloneOpenApiObject(xml),
            _ when IsSafelyShareable(value) => value,
            _ => throw new NotSupportedException(
                     $"OpenApiSchema member '{memberName}' has unsupported mutable type '{value.GetType().FullName}' for cloning. " +
                     "Update the schema clone logic before using operation-local schema mutations.")
        };
    }

    static T CloneOpenApiObject<T>(T source) where T : new()
    {
        var clone = new T();

        foreach (var property in GetCloneableProperties(typeof(T)))
            property.SetValue(clone, CloneSchemaMemberValue(property.Name, property.GetValue(source)));

        return clone;
    }

    [UnconditionalSuppressMessage("aot", "IL2070")]
    static PropertyInfo[] GetCloneableProperties(Type type)
        => _cloneablePropertiesCache.GetOrAdd(
            type,
            static t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(p => p is { CanRead: true, CanWrite: true } && p.GetIndexParameters().Length == 0)
                         .ToArray());

    static Dictionary<string, JsonNode>? CloneJsonNodeDictionary(IDictionary<string, JsonNode>? nodes)
        => CloneDictionary(nodes, static node => node.DeepClone());

    static Dictionary<string, HashSet<string>>? CloneStringSetDictionary(IDictionary<string, HashSet<string>>? sets)
        => CloneDictionary(sets, static set => new HashSet<string>(set, StringComparer.Ordinal));

    static Dictionary<string, IOpenApiExtension>? CloneExtensionsDictionary(IDictionary<string, IOpenApiExtension>? extensions)
    {
        if (extensions is null)
            return null;

        if (extensions.Count > 0)
        {
            throw new NotSupportedException(
                "OpenApiSchema extensions cannot be safely cloned without sharing mutable extension instances. " +
                "Update the schema clone logic before using operation-local schema mutations with schema extensions.");
        }

        return new(StringComparer.Ordinal);
    }

    static Dictionary<string, object>? CloneMetadataDictionary(string memberName, IDictionary<string, object>? metadata)
        => CloneDictionary(metadata, value => CloneSchemaMemberValue(memberName, value) ?? new object());

    static Dictionary<string, IOpenApiSchema>? CloneSchemaDictionary(IDictionary<string, IOpenApiSchema>? schemas)
        => CloneDictionary(schemas, static schema => CloneNestedSchema(schema)!);

    static Dictionary<string, TValue>? CloneDictionary<TSource, TValue>(IDictionary<string, TSource>? source, Func<TSource, TValue> cloneValue)
    {
        if (source is null)
            return null;

        var cloned = new Dictionary<string, TValue>(source.Count, StringComparer.Ordinal);

        foreach (var (key, value) in source)
            cloned[key] = cloneValue(value);

        return cloned;
    }

    static List<IOpenApiSchema>? CloneSchemaList(IList<IOpenApiSchema>? schemas)
    {
        if (schemas is null)
            return null;

        var cloned = new List<IOpenApiSchema>(schemas.Count);

        foreach (var schema in schemas)
            cloned.Add(CloneNestedSchema(schema)!);

        return cloned;
    }

    static IOpenApiSchema? CloneNestedSchema(IOpenApiSchema? schema)
        => schema switch
        {
            OpenApiSchema concreteSchema => CloneConcreteSchemaFallback(concreteSchema),
            OpenApiSchemaReference schemaRef => schemaRef,
            null => null,
            _ => schema
        };

    static IOpenApiSchema? ResolveSchemaReference(OpenApiSchemaReference schemaRef)
    {
        if (schemaRef.Target is { } target)
            return target;

        var refId = schemaRef.GetReferenceId();

        if (string.IsNullOrEmpty(refId) || schemaRef.Reference.IsExternal)
            return null;

        return schemaRef.Reference.HostDocument?.Components?.Schemas?.TryGetValue(refId, out var schema) == true
                   ? schema
                   : null;
    }

    static List<JsonNode>? CloneJsonNodeList(IList<JsonNode>? nodes)
    {
        if (nodes is null)
            return null;

        var cloned = new List<JsonNode>(nodes.Count);

        foreach (var node in nodes)
            cloned.Add(node.DeepClone());

        return cloned;
    }

    static bool IsSafelyShareable(object value)
    {
        var type = value.GetType();

        return type.IsValueType || value is string or Type or Uri;
    }
}

readonly record struct OperationSchemaMutationContext(SharedContext SharedContext, string OperationKey);

static class OpenApiSchemaReferenceExtensions
{
    internal static string? GetReferenceId(this OpenApiSchemaReference schemaRef)
        => schemaRef.Reference.Id ?? schemaRef.Id;
}