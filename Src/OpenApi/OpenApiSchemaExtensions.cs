using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static partial class OperationSchemaHelpers
{
    static readonly ConstructorInfo? _openApiSchemaCopyCtor = typeof(OpenApiSchema).GetConstructor([typeof(IOpenApiSchema)]);
    static readonly PropertyInfo[] _openApiSchemaProperties = typeof(OpenApiSchema).GetProperties(BindingFlags.Public | BindingFlags.Instance);

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

        internal OpenApiSchema? CloneAsConcreteSchema()
            => schema switch
            {
                OpenApiSchema concreteSchema => CloneConcreteSchema(concreteSchema),
                OpenApiSchemaReference { Target: IOpenApiSchema target } => CloneConcreteSchema(target),
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

        internal OpenApiSchema? EnsureOperationLocalSchemaIfShared(SharedContext sharedCtx)
        {
            if (mediaType.Schema is not OpenApiSchemaReference schemaRef ||
                GetReferenceId(schemaRef) is not { } refId ||
                !sharedCtx.SharedRequestSchemaRefs.Contains(refId))
                return mediaType.Schema.ResolveSchema();

            return mediaType.EnsureOperationLocalSchema();
        }
    }

    static OpenApiSchema CloneConcreteSchema(IOpenApiSchema schema)
    {
        if (_openApiSchemaCopyCtor?.Invoke([schema]) is OpenApiSchema cloned)
            return cloned;

        return CloneConcreteSchemaFallback(schema.ResolveSchema() ?? (schema as OpenApiSchema) ?? StringSchema());
    }

    static OpenApiSchema CloneConcreteSchemaFallback(OpenApiSchema schema)
    {
        var clone = new OpenApiSchema();

        foreach (var property in _openApiSchemaProperties)
        {
            if (!property.CanRead || !property.CanWrite)
                continue;

            property.SetValue(clone, CloneSchemaMemberValue(property.GetValue(schema)));
        }

        return clone;
    }

    static object? CloneSchemaMemberValue(object? value)
    {
        if (value is null)
            return null;

        return value switch
        {
            JsonNode node => node.DeepClone(),
            OpenApiSchema concreteSchema => CloneConcreteSchemaFallback(concreteSchema),
            OpenApiSchemaReference schemaRef => schemaRef,
            IDictionary<string, IOpenApiSchema> schemaDictionary => CloneSchemaDictionary(schemaDictionary),
            IList<IOpenApiSchema> schemaList => CloneSchemaList(schemaList),
            ISet<string> requiredSet => new HashSet<string>(requiredSet),
            IList<JsonNode> jsonNodes => CloneJsonNodeList(jsonNodes),
            _ => value
        };
    }

    static Dictionary<string, IOpenApiSchema>? CloneSchemaDictionary(IDictionary<string, IOpenApiSchema>? schemas)
    {
        if (schemas is null)
            return null;

        var cloned = new Dictionary<string, IOpenApiSchema>(schemas.Count, StringComparer.Ordinal);

        foreach (var (key, value) in schemas)
            cloned[key] = CloneNestedSchema(value)!;

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

    static List<JsonNode>? CloneJsonNodeList(IList<JsonNode>? nodes)
    {
        if (nodes is null)
            return null;

        var cloned = new List<JsonNode>(nodes.Count);

        foreach (var node in nodes)
        {
            if (node is not null)
                cloned.Add(node.DeepClone());
        }

        return cloned;
    }

    static string? GetReferenceId(OpenApiSchemaReference schemaRef)
        => schemaRef.Reference.Id ?? schemaRef.Id;
}
