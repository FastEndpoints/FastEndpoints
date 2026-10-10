using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class OpenApiSchemaTraversal
{
    internal static void CollectReferences(IOpenApiSchema? schema, HashSet<string> refs, Queue<string> pendingRefs)
    {
        switch (schema)
        {
            case null:
                return;
            case OpenApiSchemaReference schemaRef:
            {
                var refId = schemaRef.GetReferenceId();

                if (!string.IsNullOrEmpty(refId) && refs.Add(refId))
                    pendingRefs.Enqueue(refId);

                return;
            }
            case OpenApiSchema concreteSchema:
                TraverseChildSchemas(concreteSchema, child => CollectReferences(child, refs, pendingRefs));

                break;
        }
    }

    internal static void Visit(IOpenApiSchema? schema, Action<IOpenApiSchema?> visit)
    {
        visit(schema);

        if (schema is OpenApiSchema concreteSchema)
            TraverseChildSchemas(concreteSchema, static (child, callback) => Visit(child, callback), visit);
    }

    internal static IOpenApiSchema? Rewrite(IOpenApiSchema? schema, Func<IOpenApiSchema?, IOpenApiSchema?> rewrite)
    {
        var rewritten = rewrite(schema);

        if (rewritten is not OpenApiSchema concreteSchema)
            return rewritten;

        RewriteChildSchemas(concreteSchema, rewrite);

        return rewritten;
    }

    static void TraverseChildSchemas(OpenApiSchema schema, Action<IOpenApiSchema?> visit)
        => TraverseChildSchemas(schema, static (child, callback) => callback(child), visit);

    static void TraverseChildSchemas<TState>(OpenApiSchema schema, Action<IOpenApiSchema?, TState> visit, TState state)
    {
        if (schema.Properties is { Count: > 0 })
        {
            foreach (var childSchema in schema.Properties.Values)
                visit(childSchema, state);
        }

        visit(schema.Items, state);
        visit(schema.AdditionalProperties, state);
        visit(schema.Not, state);

        if (schema.AllOf is { Count: > 0 })
            VisitSchemas(schema.AllOf, visit, state);

        if (schema.OneOf is { Count: > 0 })
            VisitSchemas(schema.OneOf, visit, state);

        if (schema.AnyOf is { Count: > 0 })
            VisitSchemas(schema.AnyOf, visit, state);

        if (schema.PatternProperties is { Count: > 0 })
        {
            foreach (var childSchema in schema.PatternProperties.Values)
                visit(childSchema, state);
        }

        if (schema.Definitions is { Count: > 0 })
        {
            foreach (var childSchema in schema.Definitions.Values)
                visit(childSchema, state);
        }

        if (schema.Discriminator?.Mapping is { Count: > 0 })
        {
            foreach (var mappedSchema in schema.Discriminator.Mapping.Values)
                visit(mappedSchema, state);
        }
    }

    static void RewriteChildSchemas(OpenApiSchema schema, Func<IOpenApiSchema?, IOpenApiSchema?> rewrite)
    {
        if (schema.Properties is { Count: > 0 })
        {
            foreach (var (key, childSchema) in schema.Properties.ToArray())
                schema.Properties[key] = Rewrite(childSchema, rewrite)!;
        }

        schema.Items = Rewrite(schema.Items, rewrite);
        schema.AdditionalProperties = Rewrite(schema.AdditionalProperties, rewrite);
        schema.Not = Rewrite(schema.Not, rewrite);

        if (schema.AllOf is { Count: > 0 })
            RewriteSchemaList(schema.AllOf, rewrite);

        if (schema.OneOf is { Count: > 0 })
            RewriteSchemaList(schema.OneOf, rewrite);

        if (schema.AnyOf is { Count: > 0 })
            RewriteSchemaList(schema.AnyOf, rewrite);

        if (schema.PatternProperties is { Count: > 0 })
        {
            foreach (var (key, childSchema) in schema.PatternProperties.ToArray())
                schema.PatternProperties[key] = Rewrite(childSchema, rewrite)!;
        }

        if (schema.Definitions is { Count: > 0 })
        {
            foreach (var (key, childSchema) in schema.Definitions.ToArray())
                schema.Definitions[key] = Rewrite(childSchema, rewrite)!;
        }

        if (schema.Discriminator?.Mapping is { Count: > 0 })
        {
            foreach (var (key, mappedSchema) in schema.Discriminator.Mapping.ToArray())
            {
                if (Rewrite(mappedSchema, rewrite) is OpenApiSchemaReference rewrittenSchemaRef)
                    schema.Discriminator.Mapping[key] = rewrittenSchemaRef;
            }
        }
    }

    static void VisitSchemas<TState>(IEnumerable<IOpenApiSchema?> schemas, Action<IOpenApiSchema?, TState> visit, TState state)
    {
        foreach (var schema in schemas)
            visit(schema, state);
    }

    static void RewriteSchemaList(IList<IOpenApiSchema> schemas, Func<IOpenApiSchema?, IOpenApiSchema?> rewrite)
    {
        for (var i = 0; i < schemas.Count; i++)
            schemas[i] = Rewrite(schemas[i], rewrite)!;
    }

}
