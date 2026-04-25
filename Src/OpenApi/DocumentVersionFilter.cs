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
