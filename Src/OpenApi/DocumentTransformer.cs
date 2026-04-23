using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed partial class DocumentTransformer : IOpenApiDocumentTransformer
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

    readonly DocumentOptions _opts;
    readonly SharedContext _sharedCtx;
    readonly int _maxEpVer;
    readonly int _minEpVer;
    readonly int _docRelVer;
    readonly bool _showDeprecated;

    public DocumentTransformer(DocumentOptions opts, SharedContext sharedCtx)
    {
        _opts = opts;
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

    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        _opts.Services ??= context.ApplicationServices;
        _sharedCtx.ResolveNamingPolicy(context.ApplicationServices);

        if (_opts.Title is not null)
            document.Info.Title = _opts.Title;

        if (_opts.Version is not null)
            document.Info.Version = _opts.Version;

        ApplyVersionFiltering(document);
        AddSecuritySchemes(document);
        FixOperationSecurity(document);
        AddTagDescriptions(document);
        RemoveHeaderValueSchemas(document);
        document.RemoveFormFileSchemas();
        ApplyPathNamingPolicy(document);
        await NormalizeSchemas(document, context, cancellationToken);

        if (_opts.TagDescriptions is null)
            document.Tags?.Clear();

        document.SortPaths();
        document.SortSchemas();
        document.SortResponses();

    }

    async Task NormalizeSchemas(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        await document.AddMissingSchemas(_sharedCtx, context, cancellationToken);
        document.RemoveUnreferencedSchemas();
    }

    void ApplyVersionFiltering(OpenApiDocument document)
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

        bool IsInRequestedRange(OperationMeta op)
            => _docRelVer > 0
                   ? op.StartingReleaseVersion <= _docRelVer
                   : op.Version >= _minEpVer && op.Version <= _maxEpVer;

        bool IsDeprecated(OperationMeta op, int latestVersion)
            => _docRelVer > 0
                   ? (op.DeprecatedAt > 0 && _docRelVer >= op.DeprecatedAt) || op.Version != latestVersion
                   : _maxEpVer >= op.DeprecatedAt && op.DeprecatedAt != 0;
    }

    void AddSecuritySchemes(OpenApiDocument document)
    {
        if (_opts.AuthSchemes.Count == 0)
            return;

        document.Components ??= new();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        foreach (var auth in _opts.AuthSchemes)
            document.Components.SecuritySchemes[auth.Name] = auth.Scheme;
    }

    void FixOperationSecurity(OpenApiDocument document)
    {
        if (_sharedCtx.SecurityRequirements.IsEmpty)
            return;

        foreach (var (path, pathItem) in document.Paths)
        {
            if (pathItem.Operations is null)
                continue;

            foreach (var (method, operation) in pathItem.Operations)
            {
                var opKey = $"{method.ToString().ToUpperInvariant()}:{path}";

                if (!_sharedCtx.SecurityRequirements.TryGetValue(opKey, out var securityEntries))
                    continue;

                // replace any framework-generated empty security with properly referenced requirements
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

    void AddTagDescriptions(OpenApiDocument document)
    {
        if (_opts.TagDescriptions is null)
            return;

        var dict = new Dictionary<string, string>();
        _opts.TagDescriptions(dict);

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

    static void RemoveHeaderValueSchemas(OpenApiDocument document)
    {
        if (document.Components?.Schemas is null)
            return;

        const string stringSegmentKey = "MicrosoftExtensionsPrimitivesStringSegment";
        var headerRemoved = false;

        foreach (var s in document.Components.Schemas.Keys.ToArray())
        {
            if (IsHeaderValueSchema(s))
            {
                document.Components.Schemas.Remove(s);
                headerRemoved = true;
            }
        }

        if (headerRemoved)
            document.Components.Schemas.Remove(stringSegmentKey);

        static bool IsHeaderValueSchema(string schemaKey)
            => _frameworkHeaderValueSchemaKeys.Contains(schemaKey);
    }

    void ApplyPathNamingPolicy(OpenApiDocument document)
    {
        if (!_opts.UsePropertyNamingPolicy)
            return;

        var policy = _sharedCtx.NamingPolicy;

        if (policy is null)
            return;

        var renames = new List<(string OldPath, string NewPath)>();

        foreach (var path in document.Paths.Keys)
        {
            var newPath = Regex().Replace(
                path,
                m =>
                {
                    var paramName = m.Groups[1].Value;

                    return $"{{{policy.ConvertName(paramName)}}}";
                });

            if (newPath != path)
                renames.Add((path, newPath));
        }

        foreach (var (oldPath, newPath) in renames)
        {
            if (document.Paths.Remove(oldPath, out var pathItem))
                document.Paths[newPath] = pathItem;
        }
    }

    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex Regex();
}
