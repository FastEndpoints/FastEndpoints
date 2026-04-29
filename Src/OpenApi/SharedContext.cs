using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FastEndpoints.OpenApi;

/// <summary>
/// shared state between operation and document transformers for a single document
/// </summary>
internal class SharedContext
{
    static readonly FrozenSet<string> _emptyRequestSchemaRefs = Array.Empty<string>().ToFrozenSet(StringComparer.Ordinal);

    internal JsonNamingPolicy? NamingPolicy;
    readonly object _requestSchemaSharingLock = new();
    FrozenSet<string>? _sharedRequestSchemaRefs;

    internal IReadOnlySet<string> SharedRequestSchemaRefs => Volatile.Read(ref _sharedRequestSchemaRefs) ?? _emptyRequestSchemaRefs;

    internal JsonNamingPolicy? ResolveNamingPolicy(IServiceProvider services)
        => NamingPolicy ??= services.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions.PropertyNamingPolicy;

    internal void InitializeSharedRequestSchemaRefs(IServiceProvider services, DocumentOptions docOpts)
    {
        if (Volatile.Read(ref _sharedRequestSchemaRefs) is not null)
            return;

        lock (_requestSchemaSharingLock)
        {
            if (_sharedRequestSchemaRefs is not null)
                return;

            Volatile.Write(ref _sharedRequestSchemaRefs, BuildSharedRequestSchemaRefs(services, docOpts));
        }
    }

    static FrozenSet<string> BuildSharedRequestSchemaRefs(IServiceProvider services, DocumentOptions docOpts)
    {
        var empty = _emptyRequestSchemaRefs;

        var serviceResolver = services.GetService<IServiceResolver>();

        if (serviceResolver is null)
            return empty;

        var endpointData = serviceResolver.Resolve<EndpointData>();

        if (endpointData is null)
            return empty;

        var includedRefCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var epDef in endpointData.Found)
        {
            if (!DocumentVersionFilter.IncludesEndpoint(epDef, docOpts) || epDef.ReqDtoType == Types.EmptyRequest)
                continue;

            var refId = SchemaNameGenerator.GetReferenceId(epDef.ReqDtoType, docOpts.ShortSchemaNames);

            if (refId is null)
                continue;

            includedRefCounts.TryGetValue(refId, out var count);
            includedRefCounts[refId] = count + 1;
        }

        return includedRefCounts.Where(kvp => kvp.Value > 1)
                                .Select(kvp => kvp.Key)
                                .ToFrozenSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// key: "METHOD:/path", value: metadata about the operation
    /// </summary>
    internal ConcurrentDictionary<string, OperationMeta> Operations { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// key: "METHOD:/path", value: immutable snapshot of (schemeName, scopes) tuples for security requirements
    /// </summary>
    internal ConcurrentDictionary<string, (string SchemeName, string[] Scopes)[]> SecurityRequirements { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// types that need schemas generated in the document but weren't picked up by ApiExplorer.
    /// key: schema reference id, value: the CLR type
    /// </summary>
    internal ConcurrentDictionary<string, Type> MissingSchemaTypes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// request DTO schemas whose body was promoted to a [FromBody]/[FromForm] property schema.
    /// </summary>
    internal ConcurrentDictionary<string, byte> PromotedRequestWrapperSchemaRefs { get; } = new(StringComparer.OrdinalIgnoreCase);
}

internal class OperationMeta
{
    public required string OperationKey { get; init; }
    public required string DocumentPath { get; init; }
    public required string HttpMethod { get; init; }
    public required int Version { get; init; }
    public required int StartingReleaseVersion { get; init; }
    public required int DeprecatedAt { get; init; }
    public required bool IsFastEndpoint { get; init; }
}