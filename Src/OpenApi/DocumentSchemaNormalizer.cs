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

        foreach (var s in document.Components.Schemas.Keys.ToArray())
            headerRemoved |= _frameworkHeaderValueSchemaKeys.Contains(s) && document.Components.Schemas.Remove(s);

        if (headerRemoved)
            document.Components.Schemas.Remove(stringSegmentKey);
    }
}