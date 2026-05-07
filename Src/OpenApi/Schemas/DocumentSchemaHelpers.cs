using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class DocumentSchemaHelpers
{
    #region ReferencedSchemaRefs

    internal static HashSet<string> GetReferencedSchemaRefs(this OpenApiDocument document)
        => OpenApiSchemaReferenceCollector.GetReferencedSchemaRefs(document);

    #endregion

    #region Pruning

    internal static void RemoveUnreferencedSchemas(this OpenApiDocument document, HashSet<string> referencedSchemas)
    {
        if (document.Components?.Schemas is not { Count: > 0 } schemas)
            return;

        foreach (var key in schemas.Keys.ToArray())
        {
            if (!referencedSchemas.Contains(key))
                schemas.Remove(key);
        }
    }

    internal static void RemoveUnreferencedSchemas(this OpenApiDocument document)
        => document.RemoveUnreferencedSchemas(document.GetReferencedSchemaRefs());

    internal static void RemovePromotedRequestWrapperSchemas(this OpenApiDocument document, SharedContext sharedCtx, HashSet<string> referencedSchemas)
    {
        if (sharedCtx.PromotedRequestWrapperSchemaRefs.IsEmpty || document.Components?.Schemas is not { Count: > 0 } schemas)
            return;

        foreach (var refId in sharedCtx.PromotedRequestWrapperSchemaRefs.Keys)
        {
            if (!referencedSchemas.Contains(refId))
                schemas.Remove(refId);
        }
    }

    internal static void RemovePromotedRequestWrapperSchemas(this OpenApiDocument document, SharedContext sharedCtx)
        => document.RemovePromotedRequestWrapperSchemas(sharedCtx, document.GetReferencedSchemaRefs());

    internal static void RemoveFormFileSchemas(this OpenApiDocument document)
    {
        OpenApiSchemaGraphTransformer.TransformDocumentSchemas(document, RewriteFormFileSchema);

        if (document.Components?.Schemas is null)
            return;

        foreach (var key in document.Components.Schemas.Keys.ToArray())
        {
            if (key.Contains("IFormFile", StringComparison.Ordinal))
                document.Components.Schemas.Remove(key);
        }
    }

    internal static async Task AddMissingSchemas(this OpenApiDocument document, SharedContext sharedCtx, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        if (sharedCtx.MissingSchemaTypes.IsEmpty)
            return;

        var schemas = document.EnsureComponentSchemas();

        foreach (var (refId, type) in sharedCtx.MissingSchemaTypes)
        {
            if (schemas.ContainsKey(refId))
                continue;

            var schema = await context.GetOrCreateSchemaAsync(type, parameterDescription: null, ct);
            schemas.TryAdd(refId, schema);
        }
    }

    #endregion

    #region Sorting

    internal static void SortPaths(this OpenApiDocument document)
        => document.Paths.SortByKey();

    internal static void SortResponses(this OpenApiDocument document)
    {
        foreach (var (_, pathItem) in document.Paths)
        {
            if (pathItem.Operations is null)
                continue;

            foreach (var (_, operation) in pathItem.Operations)
            {
                operation.Responses?.SortByKey();
            }
        }
    }

    internal static void SortSchemas(this OpenApiDocument document)
    {
        if (document.Components?.Schemas is null)
            return;

        document.Components.Schemas.SortByKey();
    }

    #endregion

    #region Variants

    internal static void AddOperationSchemaVariants(this OpenApiDocument document, SharedContext sharedCtx)
    {
        if (sharedCtx.OperationSchemaVariants.IsEmpty)
            return;

        var schemas = document.EnsureComponentSchemas();

        foreach (var (refId, schema) in sharedCtx.OperationSchemaVariants)
            schemas.TryAdd(refId, schema);
    }

    static IDictionary<string, IOpenApiSchema> EnsureComponentSchemas(this OpenApiDocument document)
    {
        document.Components ??= new();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

        return document.Components.Schemas;
    }

    internal static void DeduplicateOperationSchemaVariants(this OpenApiDocument document, SharedContext sharedCtx)
    {
        if (sharedCtx.OperationSchemaVariants.IsEmpty || document.Components?.Schemas is not { Count: > 0 } schemas)
            return;

        var variantIds = sharedCtx.OperationSchemaVariants.Keys
                                  .Where(schemas.ContainsKey)
                                  .ToHashSet(StringComparer.Ordinal);

        if (variantIds.Count == 0)
            return;

        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var groupedVariantIds = variantIds.GroupBy(GetOperationVariantSourceRefId, StringComparer.Ordinal)
                                  .Select(static g => new OrderedVariantGroup(g.Key, g.Order(StringComparer.Ordinal).ToArray()))
                                  .ToArray();
        var changed = true;
        var aliasRevision = 0;

        while (changed)
        {
            changed = false;
            var signatureCache = new Dictionary<SchemaSignatureCacheKey, string>();

            foreach (var group in groupedVariantIds)
            {
                var signatureToRefId = new Dictionary<string, string>(StringComparer.Ordinal);

                if (schemas.TryGetValue(group.SourceRefId, out var sourceSchema) && sourceSchema is OpenApiSchema concreteSourceSchema)
                    signatureToRefId[SchemaSignatureBuilder.GetSchemaSignature(group.SourceRefId, concreteSourceSchema, aliases, signatureCache, aliasRevision)] =
                        group.SourceRefId;

                foreach (var refId in group.VariantIds)
                {
                    if (aliases.ContainsKey(refId) || !schemas.TryGetValue(refId, out var iSchema) || iSchema is not OpenApiSchema schema)
                        continue;

                    var signature = SchemaSignatureBuilder.GetSchemaSignature(refId, schema, aliases, signatureCache, aliasRevision);

                    if (signatureToRefId.TryGetValue(signature, out var canonicalRefId))
                    {
                        aliases[refId] = ResolveAlias(canonicalRefId, aliases);
                        changed = true;
                        aliasRevision++;
                    }
                    else
                        signatureToRefId[signature] = refId;
                }
            }
        }

        if (aliases.Count == 0)
            return;

        RewriteSchemaRefs(document, aliases);

        foreach (var duplicateRefId in aliases.Keys)
            schemas.Remove(duplicateRefId);
    }

    internal static void CollapseExclusiveOperationSchemaVariants(this OpenApiDocument document, SharedContext sharedCtx)
    {
        if (sharedCtx.OperationSchemaVariants.IsEmpty || document.Components?.Schemas is not { Count: > 0 } schemas)
            return;

        var variantGroups = sharedCtx.EnumerateOperationSchemaVariants()
                                   .GroupBy(static kvp => kvp.Key.SourceRefId, StringComparer.Ordinal)
                                   .Select(g => new
                                   {
                                       SourceRefId = g.Key,
                                       Variants = g.Where(kvp => schemas.ContainsKey(kvp.Value.RefId)).ToArray()
                                   })
                                   .Where(static g => g.Variants.Length > 0)
                                   .ToArray();

        if (variantGroups.Length == 0)
            return;

        var referencedSchemas = document.GetReferencedSchemaRefs();
        var collapseAliases = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var group in variantGroups)
        {
            var sourceRefId = group.SourceRefId;
            var sourceExists = schemas.ContainsKey(sourceRefId);

            if (group.Variants.Length != 1 ||
                (sourceExists && referencedSchemas.Contains(sourceRefId)))
                continue;

            var variantRefId = group.Variants[0].Value.RefId;

            if (!schemas.TryGetValue(variantRefId, out var variantSchema) || VariantReferencesRefId(variantSchema, sourceRefId))
                continue;

            schemas[sourceRefId] = variantSchema;
            collapseAliases[variantRefId] = sourceRefId;
        }

        if (collapseAliases.Count == 0)
            return;

        RewriteSchemaRefs(document, collapseAliases);

        foreach (var variantRefId in collapseAliases.Keys)
            schemas.Remove(variantRefId);
    }

    static string GetOperationVariantSourceRefId(string refId)
    {
        var suffixIndex = refId.LastIndexOf("__op", StringComparison.Ordinal);

        return suffixIndex < 0 ? refId : refId[..suffixIndex];
    }

    internal static string ResolveAlias(string refId, Dictionary<string, string> aliases)
    {
        if (!aliases.TryGetValue(refId, out var targetRefId))
            return refId;

        List<string>? traversedAliases = null;

        do
        {
            (traversedAliases ??= []).Add(refId);
            refId = targetRefId;
        }
        while (aliases.TryGetValue(refId, out targetRefId));

        foreach (var traversedAlias in traversedAliases)
            aliases[traversedAlias] = refId;

        return refId;
    }

    static bool VariantReferencesRefId(IOpenApiSchema? schema, string targetRefId)
    {
        var refs = new HashSet<string>(StringComparer.Ordinal);
        var pendingRefs = new Queue<string>();
        OpenApiSchemaReferenceCollector.CollectSchemaRefs(schema, refs, pendingRefs);

        return refs.Contains(targetRefId);
    }

    static void RewriteSchemaRefs(OpenApiDocument document, Dictionary<string, string> aliases)
        => OpenApiSchemaGraphTransformer.TransformDocumentSchemas(document, schema => RewriteSchemaRef(schema, aliases));

    static IOpenApiSchema? RewriteSchemaRef(IOpenApiSchema? schema, Dictionary<string, string> aliases)
    {
        if (schema is OpenApiSchemaReference schemaRef &&
            schemaRef.GetReferenceId() is { } refId &&
            aliases.TryGetValue(refId, out var canonicalRefId))
            return new OpenApiSchemaReference(ResolveAlias(canonicalRefId, aliases));

        return schema;
    }

    readonly record struct OrderedVariantGroup(string SourceRefId, string[] VariantIds);

    internal readonly record struct SchemaSignatureCacheKey(string RefId, int AliasRevision);

    #endregion

    #region FormFiles

    static IOpenApiSchema? RewriteFormFileSchema(IOpenApiSchema? schema)
    {
        if (IsFormFileCollectionRef(schema))
            return FormFileArraySchema();

        if (IsFormFileRef(schema))
            return FormFileBinarySchema();

        return schema;
    }

    static bool IsFormFileRef(IOpenApiSchema? schema)
        => schema is OpenApiSchemaReference schemaRef && schemaRef.GetReferenceId() is "IFormFile";

    static bool IsFormFileCollectionRef(IOpenApiSchema? schema)
        => schema is OpenApiSchemaReference schemaRef &&
           schemaRef.GetReferenceId() is { } refId &&
           (refId is "IFormFileCollection" ||
            refId.Contains("IFormFileCollection", StringComparison.Ordinal) ||
            refId.Contains("IEnumerableOfIFormFile", StringComparison.Ordinal) ||
            refId.Contains("ListOfIFormFile", StringComparison.Ordinal) ||
            refId.Contains("IFormFile[]", StringComparison.Ordinal));

    static OpenApiSchema FormFileBinarySchema()
        => new() { Type = JsonSchemaType.String, Format = "binary" };

    static OpenApiSchema FormFileArraySchema()
        => new()
        {
            Type = JsonSchemaType.Array,
            Items = FormFileBinarySchema()
        };

    #endregion
}
