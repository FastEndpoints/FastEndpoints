using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed class DocumentTransformer : IOpenApiDocumentTransformer
{
    readonly DocumentOptions _opts;
    readonly SharedContext _sharedCtx;
    readonly DocumentVersionFilter _versionFilter;

    public DocumentTransformer(DocumentOptions opts, SharedContext sharedCtx)
    {
        _opts = opts;
        _sharedCtx = sharedCtx;
        _versionFilter = new(opts, sharedCtx);
    }

    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        _opts.Services ??= context.ApplicationServices;
        _sharedCtx.ResolveNamingPolicy(context.ApplicationServices);

        if (_opts.Title is not null)
            document.Info.Title = _opts.Title;

        if (_opts.Version is not null)
            document.Info.Version = _opts.Version;

        _versionFilter.Apply(document);
        DocumentSecurityTransformer.Apply(document, _opts, _sharedCtx);
        DocumentTagTransformer.Apply(document, _opts);
        DocumentSchemaNormalizer.RemoveFrameworkSchemas(document);
        document.RemoveFormFileSchemas();
        DocumentPathNormalizer.Apply(document, _opts, _sharedCtx);
        await DocumentSchemaNormalizer.Normalize(document, _sharedCtx, context, cancellationToken);
        DocumentTagTransformer.Cleanup(document, _opts);

        document.SortPaths();
        document.SortSchemas();
        document.SortResponses();
    }
}
