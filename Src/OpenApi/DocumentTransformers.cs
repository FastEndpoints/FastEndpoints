using System.Text.RegularExpressions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed class DocumentVersionFilter
{
    readonly SharedContext _sharedCtx;
    readonly int _maxEpVer;
    readonly int _minEpVer;
    readonly int _docRelVer;
    readonly bool _showDeprecated;

    public DocumentVersionFilter(DocumentOptions opts, SharedContext sharedCtx)
    {
        _sharedCtx = sharedCtx;
        _minEpVer = opts.MinEndpointVersion;
        _maxEpVer = opts.MaxEndpointVersion;
        _docRelVer = opts.ReleaseVersion;
        _showDeprecated = opts.ShowDeprecatedOps;

        switch (_docRelVer)
        {
            case > 0 when _minEpVer > 0:
                throw new NotSupportedException(
                    $"'{nameof(DocumentOptions.MinEndpointVersion)}' cannot be used together with '{nameof(DocumentOptions.ReleaseVersion)}'." +
                    $" Please choose a single strategy when defining a swagger document!");
            case > 0 when _maxEpVer > 0:
                throw new NotSupportedException(
                    $"'{nameof(DocumentOptions.MaxEndpointVersion)}' cannot be used together with '{nameof(DocumentOptions.ReleaseVersion)}'. " +
                    $"Please choose a single strategy when defining a swagger document");
        }

        if (_maxEpVer < _minEpVer)
            throw new ArgumentException("MaxEndpointVersion must be greater than or equal to MinEndpointVersion");
    }

    public void Apply(OpenApiDocument document)
    {
        var operationsByBareRoute = new Dictionary<string, List<OperationMeta>>(StringComparer.Ordinal);
        var opsToKeep = new HashSet<(string Path, string Method)>();
        var opsToDeprecate = new HashSet<(string Path, string Method)>();

        foreach (var op in _sharedCtx.Operations.Values)
        {
            if (!op.IsFastEndpoint)
            {
                opsToKeep.Add((op.DocumentPath, op.HttpMethod));

                continue;
            }

            if (!operationsByBareRoute.TryGetValue(op.OperationKey, out var operations))
            {
                operations = [];
                operationsByBareRoute[op.OperationKey] = operations;
            }

            operations.Add(op);
        }

        foreach (var group in operationsByBareRoute.Values)
        {
            group.Sort(static (x, y) => y.Version.CompareTo(x.Version));

            var latestVersion = 0;

            for (var i = 0; i < group.Count; i++)
            {
                var candidate = group[i];

                if (candidate.StartingReleaseVersion > _docRelVer)
                    continue;

                latestVersion = candidate.Version;

                break;
            }

            for (var i = 0; i < group.Count; i++)
            {
                var op = group[i];

                if (!IsInRequestedRange(op))
                    continue;

                var isDeprecated = IsDeprecated(op, latestVersion);

                if (isDeprecated && !_showDeprecated)
                    continue;

                opsToKeep.Add((op.DocumentPath, op.HttpMethod));

                if (isDeprecated && _showDeprecated)
                    opsToDeprecate.Add((op.DocumentPath, op.HttpMethod));

                if (!_showDeprecated)
                    break;
            }
        }

        foreach (var path in document.Paths.Keys.ToArray())
        {
            if (!document.Paths.TryGetValue(path, out var pathItem) || pathItem.Operations is null)
                continue;

            foreach (var method in pathItem.Operations.Keys.ToArray())
            {
                var methodName = method.ToString().ToUpperInvariant();

                if (!opsToKeep.Contains((path, methodName)))
                {
                    pathItem.Operations.Remove(method);

                    continue;
                }

                if (opsToDeprecate.Contains((path, methodName)))
                    pathItem.Operations[method].Deprecated = true;
            }

            if (pathItem.Operations.Count == 0)
                document.Paths.Remove(path);
        }
    }

    bool IsInRequestedRange(OperationMeta op)
        => _docRelVer > 0
               ? op.StartingReleaseVersion <= _docRelVer
               : op.Version >= _minEpVer && op.Version <= _maxEpVer;

    bool IsDeprecated(OperationMeta op, int latestVersion)
        => _docRelVer > 0
               ? (op.DeprecatedAt > 0 && _docRelVer >= op.DeprecatedAt) || op.Version != latestVersion
               : _maxEpVer >= op.DeprecatedAt && op.DeprecatedAt != 0;
}

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
                        [new(schemeName, document)] = scopes
                    };
                    operation.Security.Add(requirement);
                }
            }
        }
    }
}

static class DocumentTagTransformer
{
    public static void Apply(OpenApiDocument document, DocumentOptions opts)
    {
        if (opts.TagDescriptions is null)
            return;

        var dict = new Dictionary<string, string>();
        opts.TagDescriptions(dict);

        document.Tags ??= new HashSet<OpenApiTag>();

        foreach (var kvp in dict)
        {
            var existing = document.Tags.FirstOrDefault(t => t.Name == kvp.Key);
            if (existing is not null)
                existing.Description = kvp.Value;
            else
                document.Tags.Add(new() { Name = kvp.Key, Description = kvp.Value });
        }
    }

    public static void Cleanup(OpenApiDocument document, DocumentOptions opts)
    {
        if (opts.TagDescriptions is not null || document.Tags is null)
            return;

        foreach (var tag in document.Tags.Where(static t => string.IsNullOrEmpty(t.Description) && t.ExternalDocs is null && t.Extensions is not { Count: > 0 }).ToArray())
            document.Tags.Remove(tag);
    }
}

static partial class DocumentPathNormalizer
{
    public static void NormalizeParameterNames(OpenApiDocument document)
        => RenamePaths(
            document,
            path => RouteParamRegex().Replace(
                path,
                m => $"{{{NormalizeParameterName(m.Groups[1].Value)}}}"));

    public static void Apply(OpenApiDocument document, DocumentOptions opts, SharedContext sharedCtx)
    {
        if (!opts.UsePropertyNamingPolicy)
            return;

        var policy = sharedCtx.NamingPolicy;

        if (policy is null)
            return;

        RenamePaths(
            document,
            path => RouteParamRegex().Replace(
                path,
                m => $"{{{policy.ConvertName(m.Groups[1].Value)}}}"));
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

    static string NormalizeParameterName(string segment)
    {
        var colonIdx = segment.IndexOf(':');
        var equalsIdx = segment.IndexOf('=');
        var splitIdx = colonIdx >= 0 && equalsIdx >= 0
                           ? Math.Min(colonIdx, equalsIdx)
                           : Math.Max(colonIdx, equalsIdx);
        var name = splitIdx >= 0 ? segment[..splitIdx] : segment;

        return name.TrimStart('*').TrimEnd('?');
    }

    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex RouteParamRegex();
}

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

    public static async Task Normalize(OpenApiDocument document,
                                       SharedContext sharedCtx,
                                       OpenApiDocumentTransformerContext context,
                                       CancellationToken cancellationToken)
    {
        await document.AddMissingSchemas(sharedCtx, context, cancellationToken);
        document.RemoveUnreferencedSchemas();
    }

    public static void RemoveFrameworkSchemas(OpenApiDocument document)
    {
        if (document.Components?.Schemas is null)
            return;

        const string stringSegmentKey = "MicrosoftExtensionsPrimitivesStringSegment";
        var headerRemoved = false;

        foreach (var s in document.Components.Schemas.Keys.ToArray())
        {
            if (_frameworkHeaderValueSchemaKeys.Contains(s))
            {
                document.Components.Schemas.Remove(s);
                headerRemoved = true;
            }
        }

        if (headerRemoved)
            document.Components.Schemas.Remove(stringSegmentKey);
    }
}
