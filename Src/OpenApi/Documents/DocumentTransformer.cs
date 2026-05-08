using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed class DocumentTransformer(DocumentOptions opts, SharedContext sharedCtx) : IOpenApiDocumentTransformer
{
    readonly DocumentVersionFilter _versionFilter = new(opts, sharedCtx);

    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        try
        {
            opts.Services ??= context.ApplicationServices;
            sharedCtx.ResolveNamingPolicy();

            if (opts.Title is not null)
                document.Info.Title = opts.Title;

            if (opts.Version is not null)
                document.Info.Version = opts.Version;

            DocumentPathNormalizer.NormalizeParameterNames(document);
            _versionFilter.Apply(document);
            DocumentSecurityTransformer.Apply(document, opts, sharedCtx);
            DocumentTagTransformer.Apply(document, opts);
            DocumentSchemaNormalizer.RemoveFrameworkSchemas(document);
            document.AddOperationSchemaVariants(sharedCtx);
            document.RemoveFormFileSchemas();
            document.DeduplicateOperationSchemaVariants(sharedCtx);
            DocumentPathNormalizer.Apply(document, opts, sharedCtx);
            await DocumentSchemaNormalizer.Normalize(document, sharedCtx, context, cancellationToken);
            document.CollapseExclusiveOperationSchemaVariants(sharedCtx);
            DocumentTagTransformer.Cleanup(document, opts);

            document.SortPaths();
            document.SortSchemas();
            document.SortResponses();
        }
        finally
        {
            sharedCtx.ResetPerDocumentState();
        }
    }
}
