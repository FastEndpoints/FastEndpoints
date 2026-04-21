using System.Reflection;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class DocumentSchemaHelpers
{
    extension(OpenApiDocument document)
    {
        internal void RemoveUnreferencedSchemas()
        {
            if (document.Components?.Schemas is not { Count: > 0 } schemas || document.Paths is not { Count: > 0 })
                return;

            var referencedSchemas = new HashSet<string>(StringComparer.Ordinal);
            CollectReferencedSchemas(document, referencedSchemas);

            int prevCount;

            do
            {
                prevCount = referencedSchemas.Count;

                foreach (var refId in referencedSchemas.ToArray())
                {
                    if (schemas.TryGetValue(refId, out var s))
                        CollectSchemaRefs(s, referencedSchemas);
                }
            } while (referencedSchemas.Count > prevCount);

            foreach (var key in schemas.Keys.ToArray())
            {
                if (!referencedSchemas.Contains(key))
                    schemas.Remove(key);
            }
        }

        internal void RemoveFormFileSchemas()
        {
            if (document.Components?.Schemas is { Count: > 0 })
            {
                foreach (var (_, iSchema) in document.Components.Schemas)
                {
                    if (iSchema is OpenApiSchema schema)
                        InlineFormFileRefs(schema);
                }
            }

            if (document.Paths is { Count: > 0 })
            {
                foreach (var pathItem in document.Paths.Values)
                {
                    if (pathItem.Operations is null)
                        continue;

                    foreach (var op in pathItem.Operations.Values)
                    {
                        if (op.RequestBody?.Content is { Count: > 0 })
                        {
                            foreach (var content in op.RequestBody.Content.Values)
                                RewriteFormFileMediaType(content);
                        }

                        if (op.Responses is { Count: > 0 })
                        {
                            foreach (var resp in op.Responses.Values)
                            {
                                if (resp is OpenApiResponse { Content.Count: > 0 } concreteResp)
                                {
                                    foreach (var content in concreteResp.Content.Values)
                                        RewriteFormFileMediaType(content);
                                }
                            }
                        }
                    }
                }
            }

            if (document.Components?.Schemas is null)
                return;

            foreach (var key in document.Components.Schemas.Keys.ToArray())
            {
                if (key.Contains("IFormFile", StringComparison.Ordinal))
                    document.Components.Schemas.Remove(key);
            }
        }

        internal void AddAdditionalPropertiesFalse()
        {
            if (document.Components?.Schemas is not { Count: > 0 })
                return;

            foreach (var (_, iSchema) in document.Components.Schemas)
            {
                if (iSchema is not OpenApiSchema schema)
                    continue;

                if (!schema.Type.HasValue || !schema.Type.Value.HasFlag(JsonSchemaType.Object))
                    continue;

                if (schema.AdditionalProperties is not null)
                    continue;

                schema.AdditionalPropertiesAllowed = false;
            }
        }

        internal void AddMissingSchemas(SharedContext sharedCtx)
        {
            if (sharedCtx.MissingSchemaTypes.IsEmpty)
                return;

            document.Components ??= new();
            document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

            foreach (var (refId, type) in sharedCtx.MissingSchemaTypes)
            {
                if (document.Components.Schemas.ContainsKey(refId))
                    continue;

                var extraSchemas = new Dictionary<string, IOpenApiSchema>();
                var schema = BuildSchemaForType(type, extraSchemas);

                if (schema is not null)
                    document.Components.Schemas[refId] = schema;

                foreach (var (extraRefId, extraSchema) in extraSchemas)
                    document.Components.Schemas.TryAdd(extraRefId, extraSchema);
            }
        }

        internal void FlattenAllOfSchemas()
        {
            if (document.Components?.Schemas is null)
                return;

            foreach (var (_, iSchema) in document.Components.Schemas)
            {
                if (iSchema is not OpenApiSchema schema || schema.AllOf is not { Count: > 0 })
                    continue;

                var unresolved = new List<IOpenApiSchema>();

                foreach (var allOfEntry in schema.AllOf)
                {
                    var resolved = ResolveSchemaReference(allOfEntry, document.Components.Schemas);

                    if (resolved is null)
                    {
                        unresolved.Add(allOfEntry);

                        continue;
                    }

                    MergeSchemaMembers(schema, resolved);
                }

                schema.AllOf.Clear();

                foreach (var entry in unresolved)
                    schema.AllOf.Add(entry);

                if (schema.AllOf.Count == 0)
                    schema.AllOf = null;

                if (schema.Properties is { Count: > 0 })
                    schema.Type ??= JsonSchemaType.Object;
            }
        }
    }

    static IOpenApiSchema? ResolveSchemaReference(IOpenApiSchema schema, IDictionary<string, IOpenApiSchema> schemas)
    {
        if (schema is not OpenApiSchemaReference schemaRef)
            return schema;

        var refId = GetReferenceId(schemaRef);

        return !string.IsNullOrEmpty(refId) && schemas.TryGetValue(refId, out var resolvedSchema)
                   ? resolvedSchema
                   : null;
    }

    static void MergeSchemaMembers(OpenApiSchema target, IOpenApiSchema source)
    {
        if (source.Properties is not null)
        {
            target.Properties ??= new Dictionary<string, IOpenApiSchema>();

            foreach (var (propName, propSchema) in source.Properties)
                target.Properties.TryAdd(propName, propSchema);
        }

        if (source.Required is null)
            return;

        target.Required ??= new HashSet<string>();

        foreach (var req in source.Required)
            target.Required.Add(req);
    }

    internal static void SortPaths(this OpenApiDocument document)
    {
        var sorted = document.Paths.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
        document.Paths.Clear();

        foreach (var (path, pathItem) in sorted)
            document.Paths[path] = pathItem;
    }

    internal static void SortResponses(this OpenApiDocument document)
    {
        foreach (var (_, pathItem) in document.Paths)
        {
            if (pathItem.Operations is null)
                continue;

            foreach (var (_, operation) in pathItem.Operations)
            {
                if (operation.Responses is not { Count: > 1 })
                    continue;

                var sorted = operation.Responses.OrderBy(r => r.Key, StringComparer.Ordinal).ToList();
                operation.Responses.Clear();

                foreach (var (key, value) in sorted)
                    operation.Responses[key] = value;
            }
        }
    }

    internal static void SortSchemas(this OpenApiDocument document)
    {
        if (document.Components?.Schemas is null)
            return;

        var sorted = document.Components.Schemas.OrderBy(s => s.Key, StringComparer.Ordinal).ToList();
        document.Components.Schemas.Clear();

        foreach (var (key, schema) in sorted)
            document.Components.Schemas[key] = schema;
    }

    static void CollectReferencedSchemas(OpenApiDocument document, HashSet<string> refs)
    {
        foreach (var pathItem in document.Paths.Values)
        {
            if (pathItem.Operations is null)
                continue;

            foreach (var op in pathItem.Operations.Values)
            {
                if (op.Parameters is { Count: > 0 })
                {
                    CollectSchemaRefs(op.Parameters.Select(p => p.Schema), refs);

                    foreach (var param in op.Parameters)
                    {
                        if (param.Content is { Count: > 0 })
                            CollectSchemaRefs(param.Content.Values.Select(content => content.Schema), refs);
                    }
                }

                if (op.RequestBody?.Content is { Count: > 0 })
                    CollectSchemaRefs(op.RequestBody.Content.Values.Select(content => content.Schema), refs);

                if (op.Responses is not { Count: > 0 })
                    continue;

                foreach (var resp in op.Responses.Values)
                {
                    if (resp is OpenApiResponse { Content.Count: > 0 } concreteResp)
                        CollectSchemaRefs(concreteResp.Content.Values.Select(content => content.Schema), refs);
                }
            }
        }
    }

    static void CollectSchemaRefs(IEnumerable<IOpenApiSchema?> schemas, HashSet<string> refs)
    {
        foreach (var schema in schemas)
            CollectSchemaRefs(schema, refs);
    }

    static void CollectSchemaRefs(IOpenApiSchema? schema, HashSet<string> refs)
    {
        switch (schema)
        {
            case null:
                return;
            case OpenApiSchemaReference schemaRef:
            {
                var refId = GetReferenceId(schemaRef);

                if (!string.IsNullOrEmpty(refId))
                    refs.Add(refId);

                return;
            }
            case OpenApiSchema s:
            {
                if (s.Properties is { Count: > 0 })
                    CollectSchemaRefs(s.Properties.Values, refs);

                CollectSchemaRefs([s.Items, s.AdditionalProperties], refs);

                if (s.AllOf is { Count: > 0 })
                    CollectSchemaRefs(s.AllOf, refs);

                if (s.OneOf is { Count: > 0 })
                    CollectSchemaRefs(s.OneOf, refs);

                if (s.AnyOf is { Count: > 0 })
                    CollectSchemaRefs(s.AnyOf, refs);

                break;
            }
        }
    }

    static void InlineFormFileRefs(OpenApiSchema schema)
    {
        if (schema.Properties is { Count: > 0 })
        {
            foreach (var propName in schema.Properties.Keys.ToArray())
            {
                var propSchema = schema.Properties[propName];
                if (RewriteFormFileSchema(propSchema) is { } rewrittenSchema)
                    schema.Properties[propName] = rewrittenSchema;
            }
        }

        schema.Items = RewriteFormFileSchema(schema.Items);
        schema.AdditionalProperties = RewriteFormFileSchema(schema.AdditionalProperties);

        if (schema.AllOf is { Count: > 0 })
            RewriteFormFileSchemaList(schema.AllOf);

        if (schema.OneOf is { Count: > 0 })
            RewriteFormFileSchemaList(schema.OneOf);

        if (schema.AnyOf is { Count: > 0 })
            RewriteFormFileSchemaList(schema.AnyOf);
    }

    static void RewriteFormFileMediaType(OpenApiMediaType content)
    {
        content.Schema = RewriteFormFileSchema(content.Schema);
    }

    static IOpenApiSchema? RewriteFormFileSchema(IOpenApiSchema? schema)
    {
        if (IsFormFileRef(schema))
            return FormFileBinarySchema();

        if (schema is OpenApiSchema concreteSchema)
            InlineFormFileRefs(concreteSchema);

        return schema;
    }

    static void RewriteFormFileSchemaList(IList<IOpenApiSchema> schemas)
    {
        for (var i = 0; i < schemas.Count; i++)
            schemas[i] = RewriteFormFileSchema(schemas[i])!;
    }

    static bool IsFormFileRef(IOpenApiSchema? schema)
        => schema is OpenApiSchemaReference schemaRef &&
           GetReferenceId(schemaRef) is { } refId &&
           (refId is "IFormFile" or "IFormFileCollection" ||
            refId.Contains("IFormFile", StringComparison.Ordinal));

    static string? GetReferenceId(OpenApiSchemaReference schemaRef)
        => schemaRef.Reference.Id ?? schemaRef.Id;

    static OpenApiSchema FormFileBinarySchema()
        => new() { Type = JsonSchemaType.String, Format = "binary" };

    static OpenApiSchema? BuildSchemaForType(Type type, Dictionary<string, IOpenApiSchema>? extraSchemas = null)
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(),
            Description = XmlDocLookup.GetTypeSummary(type)
        };

        var namingPolicy = Extensions.NamingPolicy;
        extraSchemas ??= new();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition ==
                JsonIgnoreCondition.Always)
                continue;

            if (prop.IsDefined(Types.HideFromDocsAttribute))
                continue;

            var jsonNameAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            var propName = jsonNameAttr?.Name ?? namingPolicy?.ConvertName(prop.Name) ?? prop.Name;
            var propSchema = GetJsonSchemaForType(prop.PropertyType, extraSchemas);

            if (propSchema is OpenApiSchema concrete)
            {
                concrete.Description ??= XmlDocLookup.GetPropertySummary(prop);

                var defaultAttr = prop.GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>();

                if (defaultAttr?.Value is not null)
                {
                    try
                    {
                        concrete.Default = JsonNode.Parse(JsonSerializer.Serialize(defaultAttr.Value));
                    }
                    catch
                    {
                        // invalid JSON — ignore
                    }
                }
            }

            schema.Properties[propName] = propSchema;
        }

        return schema.Properties.Count > 0 ? schema : null;
    }

    static IOpenApiSchema GetJsonSchemaForType(Type type, Dictionary<string, IOpenApiSchema> extraSchemas)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;

        if (TryCreatePrimitiveSchema(actualType) is { } primitiveSchema)
            return primitiveSchema;

        if (TryGetDictionaryValueType(actualType) is { } dictionaryValueType)
        {
            return new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                AdditionalProperties = GetJsonSchemaForType(dictionaryValueType, extraSchemas)
            };
        }

        if (TryGetCollectionElementType(actualType) is { } elementType)
        {
            return new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = GetJsonSchemaForType(elementType, extraSchemas)
            };
        }

        var refId = SchemaNameGenerator.GetReferenceId(actualType, false);

        if (refId is not null && !extraSchemas.ContainsKey(refId))
        {
            extraSchemas[refId] = new OpenApiSchema { Type = JsonSchemaType.Object };
            var built = BuildSchemaForType(actualType, extraSchemas);
            if (built is not null)
                extraSchemas[refId] = built;
        }

        return refId is not null
                   ? new OpenApiSchemaReference(refId)
                   : new OpenApiSchema { Type = JsonSchemaType.Object };
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
            _ when type == typeof(TimeSpan) => new() { Type = JsonSchemaType.String, Format = "duration" },
            _ when SchemaNameGenerator.IsFormFileType(type) => new() { Type = JsonSchemaType.String, Format = "binary" },
            _ => null
        };

    static Type? TryGetDictionaryValueType(Type type)
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

    static Type? TryGetCollectionElementType(Type type)
    {
        if (type.IsArray)
            return type.GetElementType();

        if (!type.IsGenericType)
            return null;

        var genDef = type.GetGenericTypeDefinition();

        return genDef == typeof(List<>) ||
               genDef == typeof(IList<>) ||
               genDef == typeof(IEnumerable<>) ||
               genDef == typeof(ICollection<>) ||
               genDef == typeof(IReadOnlyList<>) ||
               genDef == typeof(IReadOnlyCollection<>)
                   ? type.GetGenericArguments()[0]
                   : null;
    }
}
