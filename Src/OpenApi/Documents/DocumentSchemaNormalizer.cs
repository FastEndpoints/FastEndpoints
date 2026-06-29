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
        => OpenApiSchemaGraphTransformer.TransformDocumentSchemas(document, RemoveBareNullOneOfOptions);

    static IOpenApiSchema? RemoveBareNullOneOfOptions(IOpenApiSchema? schema)
    {
        if (schema is not OpenApiSchema concreteSchema || concreteSchema.OneOf is not { Count: > 0 } oneOf)
            return schema;

        var nullOptionCount = 0;
        var nonNullOptions = new List<IOpenApiSchema>(oneOf.Count);

        foreach (var option in oneOf)
        {
            if (IsBareNullSchema(option))
                nullOptionCount++;
            else
                nonNullOptions.Add(option);
        }

        if (nullOptionCount == 0)
            return concreteSchema;

        if (nonNullOptions.Count == 0)
        {
            concreteSchema.Type = JsonSchemaType.Null;
            concreteSchema.OneOf = null;

            return concreteSchema;
        }

        if (nullOptionCount == 1 && nonNullOptions.Count == 1 && TryCreateNullableArraySchema(nonNullOptions[0]) is { } nullableArraySchema)
            return nullableArraySchema;

        if (concreteSchema.Type.HasValue)
            concreteSchema.Type = concreteSchema.Type.Value | JsonSchemaType.Null;

        return concreteSchema;
    }

    static OpenApiSchema? TryCreateNullableArraySchema(IOpenApiSchema schema)
    {
        var arraySchema = schema.CloneAsConcreteSchema();

        if (arraySchema is null || GetNonNullSchemaType(arraySchema) is not { } schemaType || !schemaType.HasFlag(JsonSchemaType.Array))
            return null;

        arraySchema.Type = schemaType | JsonSchemaType.Null;
        arraySchema.OneOf = null;

        return arraySchema;
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
