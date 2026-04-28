using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class DocumentPathNormalizer
{
    public static void NormalizeParameterNames(OpenApiDocument document)
        => RenamePaths(
            document,
            RouteTemplateHelpers.NormalizePath);

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
        var normalizedPathOrigins = new Dictionary<string, string>(document.Paths.Count, StringComparer.Ordinal);

        foreach (var path in document.Paths.Keys)
            normalizedPathOrigins[path] = path;

        foreach (var path in document.Paths.Keys)
        {
            var newPath = rename(path);

            if (newPath != path)
                renames.Add((path, newPath));
        }

        foreach (var (oldPath, newPath) in renames)
        {
            if (!document.Paths.Remove(oldPath, out var pathItem))
                continue;

            if (!document.Paths.TryGetValue(newPath, out var existingPathItem))
            {
                document.Paths[newPath] = pathItem;
                normalizedPathOrigins.Remove(oldPath);
                normalizedPathOrigins[newPath] = oldPath;

                continue;
            }

            var existingPathOrigin = normalizedPathOrigins.TryGetValue(newPath, out var origin) ? origin : newPath;

            MergePathItems(existingPathItem, pathItem, oldPath, existingPathOrigin, newPath);
            normalizedPathOrigins.Remove(oldPath);
        }
    }

    static void MergePathItems(IOpenApiPathItem target,
                               IOpenApiPathItem source,
                               string sourcePath,
                               string existingPath,
                               string normalizedPath)
    {
        if (source.Operations is not { Count: > 0 })
            return;

        if (target.Operations is null)
            throw new InvalidOperationException($"Cannot merge OpenAPI path '{normalizedPath}' because the target operations collection is missing.");

        foreach (var (method, operation) in source.Operations)
        {
            if (target.Operations.ContainsKey(method))
                throw new InvalidOperationException(
                    $"OpenAPI path normalization collision detected for '{normalizedPath}'. " +
                    $"Both '{existingPath}' and '{sourcePath}' define '{method}' operations.");

            target.Operations[method] = operation;
        }
    }
}
