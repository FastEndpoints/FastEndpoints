using Microsoft.AspNetCore.OpenApi;
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

            var referencedSchemas = document.GetReferencedSchemaRefs();

            foreach (var key in schemas.Keys.ToArray())
            {
                if (!referencedSchemas.Contains(key))
                    schemas.Remove(key);
            }
        }

        internal void RemovePromotedRequestWrapperSchemas(SharedContext sharedCtx)
        {
            if (sharedCtx.PromotedRequestWrapperSchemaRefs.IsEmpty || document.Components?.Schemas is not { Count: > 0 } schemas)
                return;

            var referencedSchemas = document.GetReferencedSchemaRefs();

            foreach (var refId in sharedCtx.PromotedRequestWrapperSchemaRefs.Keys)
            {
                if (!referencedSchemas.Contains(refId))
                    schemas.Remove(refId);
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

        internal async Task AddMissingSchemas(SharedContext sharedCtx, OpenApiDocumentTransformerContext context, CancellationToken ct)
        {
            if (sharedCtx.MissingSchemaTypes.IsEmpty)
                return;

            document.Components ??= new();
            document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

            foreach (var (refId, type) in sharedCtx.MissingSchemaTypes)
            {
                if (document.Components.Schemas.ContainsKey(refId))
                    continue;

                var schema = await context.GetOrCreateSchemaAsync(type, parameterDescription: null, ct);

                if (schema is not null)
                    document.Components.Schemas.TryAdd(refId, schema);
            }
        }

        internal HashSet<string> GetReferencedSchemaRefs()
        {
            var referencedSchemas = new HashSet<string>(StringComparer.Ordinal);
            var pendingSchemas = new Queue<string>();
            CollectReferencedSchemas(document, referencedSchemas, pendingSchemas);

            while (pendingSchemas.Count > 0)
            {
                var refId = pendingSchemas.Dequeue();

                if (document.Components?.Schemas?.TryGetValue(refId, out var s) == true)
                    CollectSchemaRefs(s, referencedSchemas, pendingSchemas);
            }

            return referencedSchemas;
        }

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

    static void CollectReferencedSchemas(OpenApiDocument document, HashSet<string> refs, Queue<string> pendingRefs)
    {
        foreach (var pathItem in document.Paths.Values)
        {
            if (pathItem.Operations is null)
                continue;

            foreach (var op in pathItem.Operations.Values)
            {
                if (op.Parameters is { Count: > 0 })
                {
                    CollectSchemaRefs(op.Parameters.Select(p => p.Schema), refs, pendingRefs);

                    foreach (var param in op.Parameters)
                    {
                        if (param.Content is { Count: > 0 })
                            CollectSchemaRefs(param.Content.Values.Select(content => content.Schema), refs, pendingRefs);
                    }
                }

                if (op.RequestBody?.Content is { Count: > 0 })
                    CollectSchemaRefs(op.RequestBody.Content.Values.Select(content => content.Schema), refs, pendingRefs);

                if (op.Responses is not { Count: > 0 })
                    continue;

                foreach (var resp in op.Responses.Values)
                {
                    if (resp.Headers is { Count: > 0 })
                        CollectSchemaRefs(resp.Headers.Values.Select(h => h.Schema), refs, pendingRefs);

                    if (resp is OpenApiResponse { Content.Count: > 0 } concreteResp)
                        CollectSchemaRefs(concreteResp.Content.Values.Select(content => content.Schema), refs, pendingRefs);
                }
            }
        }
    }

    static void CollectSchemaRefs(IEnumerable<IOpenApiSchema?> schemas, HashSet<string> refs, Queue<string> pendingRefs)
    {
        foreach (var schema in schemas)
            CollectSchemaRefs(schema, refs, pendingRefs);
    }

    static void CollectSchemaRefs(IOpenApiSchema? schema, HashSet<string> refs, Queue<string> pendingRefs)
    {
        switch (schema)
        {
            case null:
                return;
            case OpenApiSchemaReference schemaRef:
            {
                var refId = GetReferenceId(schemaRef);

                if (!string.IsNullOrEmpty(refId) && refs.Add(refId))
                    pendingRefs.Enqueue(refId);

                return;
            }
            case OpenApiSchema s:
            {
                if (s.Properties is { Count: > 0 })
                    CollectSchemaRefs(s.Properties.Values, refs, pendingRefs);

                CollectSchemaRefs([s.Items, s.AdditionalProperties], refs, pendingRefs);

                if (s.AllOf is { Count: > 0 })
                    CollectSchemaRefs(s.AllOf, refs, pendingRefs);

                if (s.OneOf is { Count: > 0 })
                    CollectSchemaRefs(s.OneOf, refs, pendingRefs);

                if (s.AnyOf is { Count: > 0 })
                    CollectSchemaRefs(s.AnyOf, refs, pendingRefs);

                if (s.Discriminator?.Mapping is { Count: > 0 })
                    CollectSchemaRefs(s.Discriminator.Mapping.Values, refs, pendingRefs);

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
        if (IsFormFileCollectionRef(schema))
            return FormFileArraySchema();

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
           refId is "IFormFile";

    static bool IsFormFileCollectionRef(IOpenApiSchema? schema)
        => schema is OpenApiSchemaReference schemaRef &&
           GetReferenceId(schemaRef) is { } refId &&
           (refId is "IFormFileCollection" ||
            refId.Contains("IFormFileCollection", StringComparison.Ordinal) ||
            refId.Contains("IEnumerableOfIFormFile", StringComparison.Ordinal) ||
            refId.Contains("ListOfIFormFile", StringComparison.Ordinal) ||
            refId.Contains("IFormFile[]", StringComparison.Ordinal));

    static string? GetReferenceId(OpenApiSchemaReference schemaRef)
        => schemaRef.Reference.Id ?? schemaRef.Id;

    static OpenApiSchema FormFileBinarySchema()
        => new() { Type = JsonSchemaType.String, Format = "binary" };

    static OpenApiSchema FormFileArraySchema()
        => new()
        {
            Type = JsonSchemaType.Array,
            Items = FormFileBinarySchema()
        };

}
