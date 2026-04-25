using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class DocumentPathNormalizer
{
    public static void NormalizeParameterNames(OpenApiDocument document)
        => RenamePaths(
            document,
            path => RouteTemplateHelpers.ReplaceParameters(path, RouteTemplateHelpers.NormalizeParameterName));

    public static void Apply(OpenApiDocument document, DocumentOptions opts, SharedContext sharedCtx)
    {
        if (!opts.UsePropertyNamingPolicy)
            return;

        var policy = sharedCtx.NamingPolicy;

        if (policy is null)
            return;

        RenamePaths(
            document,
            path => RouteTemplateHelpers.ReplaceParameters(
                path,
                segment => policy.ConvertName(RouteTemplateHelpers.NormalizeParameterName(segment))));
    }

    static void RenamePaths(OpenApiDocument document, Func<string, string> rename)
    {
        var renames = new List<(string OldPath, string NewPath)>();

        foreach (var path in document.Paths.Keys)
        {
            var newPath = rename(path);

            if (newPath != path)
                renames.Add((path, newPath));
        }

        foreach (var (oldPath, newPath) in renames)
        {
            if (document.Paths.Remove(oldPath, out var pathItem))
                document.Paths[newPath] = pathItem;
        }
    }
}
