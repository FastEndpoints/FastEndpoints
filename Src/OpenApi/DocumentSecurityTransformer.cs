using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class DocumentSecurityTransformer
{
    public static void Apply(OpenApiDocument document, DocumentOptions opts, SharedContext sharedCtx)
    {
        AddSecuritySchemes(document, opts);
        FixOperationSecurity(document, sharedCtx);
    }

    static void AddSecuritySchemes(OpenApiDocument document, DocumentOptions opts)
    {
        if (opts.AuthSchemes.Count == 0)
            return;

        document.Components ??= new();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        foreach (var auth in opts.AuthSchemes)
            document.Components.SecuritySchemes[auth.Name] = auth.Scheme;
    }

    static void FixOperationSecurity(OpenApiDocument document, SharedContext sharedCtx)
    {
        if (sharedCtx.SecurityRequirements.IsEmpty)
            return;

        foreach (var (path, pathItem) in document.Paths)
        {
            if (pathItem.Operations is null)
                continue;

            foreach (var (method, operation) in pathItem.Operations)
            {
                var opKey = $"{method.ToString().ToUpperInvariant()}:{path}";

                if (!sharedCtx.SecurityRequirements.TryGetValue(opKey, out var securityEntries))
                    continue;

                operation.Security = [];

                foreach (var (schemeName, scopes) in securityEntries)
                {
                    var requirement = new OpenApiSecurityRequirement
                    {
                        [new(schemeName, document)] = [.. scopes]
                    };
                    operation.Security.Add(requirement);
                }
            }
        }
    }
}