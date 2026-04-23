using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
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
    internal JsonNamingPolicy? NamingPolicy;
    int _requestSchemaSharingInitialized;

    internal HashSet<string> SharedRequestSchemaRefs { get; } = new(StringComparer.Ordinal);

    internal JsonNamingPolicy? ResolveNamingPolicy(IServiceProvider services)
        => NamingPolicy ??= services.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions.PropertyNamingPolicy;

    internal void InitializeSharedRequestSchemaRefs(IServiceProvider services, DocumentOptions docOpts)
    {
        if (Interlocked.Exchange(ref _requestSchemaSharingInitialized, 1) == 1)
            return;

        var serviceResolver = services.GetService<IServiceResolver>();

        if (serviceResolver is null)
            return;

        var endpointData = serviceResolver.Resolve<EndpointData>();

        if (endpointData is null)
            return;

        var includedRefCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var epDef in endpointData.Found)
        {
            if (!ShouldInclude(epDef, docOpts) || epDef.ReqDtoType == Types.EmptyRequest)
                continue;

            var refId = SchemaNameGenerator.GetReferenceId(epDef.ReqDtoType, docOpts.ShortSchemaNames);

            if (refId is null)
                continue;

            includedRefCounts.TryGetValue(refId, out var count);
            includedRefCounts[refId] = count + 1;
        }

        foreach (var (refId, count) in includedRefCounts)
        {
            if (count > 1)
                SharedRequestSchemaRefs.Add(refId);
        }

        static bool ShouldInclude(EndpointDefinition epDef, DocumentOptions docOpts)
        {
            if (docOpts.EndpointFilter?.Invoke(epDef) == false)
                return false;

            if (docOpts.ReleaseVersion > 0)
                return epDef.Version.StartingReleaseVersion <= docOpts.ReleaseVersion;

            var currentVersion = epDef.Version.Current;
            var maxVersion = docOpts.MaxEndpointVersion;
            var minVersion = docOpts.MinEndpointVersion;

            if (currentVersion < minVersion)
                return false;

            if (maxVersion > 0 && currentVersion > maxVersion)
                return false;

            return true;
        }
    }

    /// <summary>
    /// key: "METHOD:/path", value: metadata about the operation
    /// </summary>
    internal ConcurrentDictionary<string, OperationMeta> Operations { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// key: "METHOD:/path", value: list of (schemeName, scopes) tuples for security requirements
    /// </summary>
    internal ConcurrentDictionary<string, List<(string SchemeName, List<string> Scopes)>> SecurityRequirements { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// types that need schemas generated in the document but weren't picked up by ApiExplorer.
    /// key: schema reference id, value: the CLR type
    /// </summary>
    internal ConcurrentDictionary<string, Type> MissingSchemaTypes { get; } = new(StringComparer.OrdinalIgnoreCase);
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
