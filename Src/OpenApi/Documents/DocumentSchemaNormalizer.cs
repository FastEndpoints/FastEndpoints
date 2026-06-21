using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class DocumentSchemaNormalizer
{
    static readonly HashSet<string> _frameworkHeaderValueSchemaKeys =
    [
        "MicrosoftNetHttpHeadersCacheControlHeaderValue",
        "MicrosoftNetHttpHeadersContentDispositionHeaderValue",
        "MicrosoftNetHttpHeadersContentRangeHeaderValue",
        "MicrosoftNetHttpHeadersMediaTypeHeaderValue",
        "MicrosoftNetHttpHeadersRangeConditionHeaderValue",
        "MicrosoftNetHttpHeadersRangeHeaderValue",
        "MicrosoftNetHttpHeadersEntityTagHeaderValue",
        "SystemCollectionsGenericIListOfMicrosoftNetHttpHeadersMediaTypeHeaderValue",
        "SystemCollectionsGenericIListOfMicrosoftNetHttpHeadersEntityTagHeaderValue",
        "SystemCollectionsGenericIListOfMicrosoftNetHttpHeadersSetCookieHeaderValue"
    ];

    public static async Task Normalize(OpenApiDocument document, SharedContext sharedCtx, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        await document.AddMissingSchemas(sharedCtx, context, cancellationToken);
        document.RemoveBareNullOneOfOptions();
        var referencedSchemas = document.GetReferencedSchemaRefs();
        document.RemovePromotedRequestWrapperSchemas(sharedCtx, referencedSchemas);
        document.RemoveUnreferencedSchemas(referencedSchemas);
    }

    public static void RemoveFrameworkSchemas(OpenApiDocument document)
    {
        if (document.Components?.Schemas is null)
            return;

        const string stringSegmentKey = "MicrosoftExtensionsPrimitivesStringSegment";
        var headerRemoved = false;

        foreach (var key in _frameworkHeaderValueSchemaKeys)
            headerRemoved |= document.Components.Schemas.Remove(key);

        if (headerRemoved)
            document.Components.Schemas.Remove(stringSegmentKey);
    }

    static void RemoveBareNullOneOfOptions(this OpenApiDocument document)
        => OpenApiSchemaGraphTransformer.TransformDocumentSchemas(document, schema =>
        {
            if (schema is OpenApiSchema concreteSchema)
                RemoveBareNullOneOfOptions(concreteSchema);

            return schema;
        });

    static void RemoveBareNullOneOfOptions(OpenApiSchema schema)
    {
        if (schema.OneOf is not { Count: > 0 } oneOf)
            return;

        var removedNullOption = false;

        for (var i = oneOf.Count - 1; i >= 0; i--)
        {
            if (!IsBareNullSchema(oneOf[i]))
                continue;

            oneOf.RemoveAt(i);
            removedNullOption = true;
        }

        if (!removedNullOption)
            return;

        if (schema.Type.HasValue)
            schema.Type = schema.Type.Value | JsonSchemaType.Null;
        else if (oneOf.Count == 0)
            schema.Type = JsonSchemaType.Null;
        else if (InferOneOfSchemaType(oneOf) is { } inferredType)
            schema.Type = inferredType | JsonSchemaType.Null;

        if (oneOf.Count == 0)
            schema.OneOf = null;
    }

    static JsonSchemaType? InferOneOfSchemaType(IEnumerable<IOpenApiSchema> oneOf)
    {
        JsonSchemaType? inferredType = null;

        foreach (var option in oneOf)
        {
            if (GetNonNullSchemaType(option) is not { } optionType)
                continue;

            inferredType = inferredType.HasValue
                               ? inferredType.Value | optionType
                               : optionType;
        }

        return inferredType;
    }

    static JsonSchemaType? GetNonNullSchemaType(IOpenApiSchema? schema)
    {
        if (schema.ResolveSchema()?.Type is not { } schemaType)
            return null;

        var nonNullType = schemaType & ~JsonSchemaType.Null;

        return nonNullType == default ? null : nonNullType;
    }

    static bool IsBareNullSchema(IOpenApiSchema? schema)
        => schema is OpenApiSchema { Type: JsonSchemaType.Null } nullSchema &&
           nullSchema.Properties is null or { Count: 0 } &&
           nullSchema.Items is null &&
           nullSchema.AdditionalProperties is null &&
           nullSchema.Not is null &&
           nullSchema.AllOf is null or { Count: 0 } &&
           nullSchema.OneOf is null or { Count: 0 } &&
           nullSchema.AnyOf is null or { Count: 0 } &&
           nullSchema.PatternProperties is null or { Count: 0 } &&
           nullSchema.Definitions is null or { Count: 0 };
}
