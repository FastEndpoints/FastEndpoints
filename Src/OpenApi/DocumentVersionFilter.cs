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

            var latestVersion = group.FirstOrDefault(op => op.StartingReleaseVersion <= _docRelVer)?.Version ?? 0;

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

        PruneDocumentOperations(document, opsToKeep, opsToDeprecate);
    }

    static void PruneDocumentOperations(OpenApiDocument document,
                                        HashSet<(string Path, string Method)> opsToKeep,
                                        HashSet<(string Path, string Method)> opsToDeprecate)
    {
        if (document.Paths is not { Count: > 0 })
            return;

        foreach (var path in document.Paths.Keys.ToArray())
        {
            if (!document.Paths.TryGetValue(path, out var pathItem) || pathItem.Operations is null)
                continue;

            PrunePathOperations(pathItem, path, opsToKeep, opsToDeprecate);

            if (pathItem.Operations.Count == 0)
                document.Paths.Remove(path);
        }
    }

    static void PrunePathOperations(IOpenApiPathItem pathItem,
                                    string path,
                                    HashSet<(string Path, string Method)> opsToKeep,
                                    HashSet<(string Path, string Method)> opsToDeprecate)
    {
        foreach (var method in pathItem.Operations!.Keys.ToArray())
        {
            var methodName = method.ToString().ToUpperInvariant();
            var operationKey = (path, methodName);

            if (!opsToKeep.Contains(operationKey))
            {
                pathItem.Operations.Remove(method);

                continue;
            }

            if (opsToDeprecate.Contains(operationKey))
                pathItem.Operations[method].Deprecated = true;
        }
    }

    internal static bool IncludesEndpoint(EndpointDefinition epDef, DocumentOptions opts)
        => opts.EndpointFilter?.Invoke(epDef) != false &&
           IsInRequestedRange(epDef.Version.Current, epDef.Version.StartingReleaseVersion, opts);

    internal static bool IsInRequestedRange(int version, int startingReleaseVersion, DocumentOptions opts)
        => IsInRequestedRange(version, startingReleaseVersion, opts.ReleaseVersion, opts.MinEndpointVersion, opts.MaxEndpointVersion);

    bool IsInRequestedRange(OperationMeta op)
        => IsInRequestedRange(op.Version, op.StartingReleaseVersion, _docRelVer, _minEpVer, _maxEpVer);

    static bool IsInRequestedRange(int version, int startingReleaseVersion, int releaseVersion, int minVersion, int maxVersion)
        => releaseVersion > 0
               ? startingReleaseVersion <= releaseVersion
               : version >= minVersion && version <= maxVersion;

    bool IsDeprecated(OperationMeta op, int latestVersion)
        => _docRelVer > 0
               ? (op.DeprecatedAt > 0 && _docRelVer >= op.DeprecatedAt) || op.Version != latestVersion
               : _maxEpVer >= op.DeprecatedAt && op.DeprecatedAt != 0;
}
