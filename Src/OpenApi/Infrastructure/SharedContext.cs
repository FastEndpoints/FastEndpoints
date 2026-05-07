using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

/// <summary>
/// shared state between operation and document transformers for a single document
/// </summary>
internal class SharedContext
{
    internal JsonSerializerOptions? SerializerOptions;
    internal JsonNamingPolicy? NamingPolicy;

    internal JsonSerializerOptions ResolveSerializerOptions()
        => SerializerOptions ??= Cfg.SerOpts.Options;

    internal JsonNamingPolicy? ResolveNamingPolicy()
        => NamingPolicy ??= ResolveSerializerOptions().PropertyNamingPolicy;

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

    internal ConcurrentDictionary<string, OpenApiSchema> OperationSchemaVariants { get; } = new(StringComparer.Ordinal);

    readonly ConcurrentDictionary<OperationSchemaVariantKey, OperationSchemaVariant> _operationSchemaVariantKeys = new();

    internal IEnumerable<KeyValuePair<OperationSchemaVariantKey, OperationSchemaVariant>> EnumerateOperationSchemaVariants()
        => _operationSchemaVariantKeys;

    internal void ResetPerDocumentState()
    {
        Operations.Clear();
        SecurityRequirements.Clear();
        MissingSchemaTypes.Clear();
        PromotedRequestWrapperSchemaRefs.Clear();
        OperationSchemaVariants.Clear();
        _operationSchemaVariantKeys.Clear();
    }

    internal bool TryGetOperationSchemaVariant(string refId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out OpenApiSchema? schema)
        => OperationSchemaVariants.TryGetValue(refId, out schema);

    internal OperationSchemaVariant GetOrAddOperationSchemaVariant(string sourceRefId, string operationKey, string schemaKey, OpenApiSchema schema)
        => _operationSchemaVariantKeys.GetOrAdd(
            new(sourceRefId, operationKey, schemaKey),
            key =>
            {
                var refId = CreateOperationSchemaVariantRefId(key.SourceRefId, key.OperationKey, key.SchemaKey);
                OperationSchemaVariants.TryAdd(refId, schema);

                return new(refId, schema);
            });

    static string CreateOperationSchemaVariantRefId(string sourceRefId, string operationKey, string schemaKey)
    {
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{sourceRefId}:{operationKey}:{schemaKey}")))[..12];

        return $"{sourceRefId}__op{hash}";
    }
}

readonly record struct OperationSchemaVariantKey(string SourceRefId, string OperationKey, string SchemaKey);

internal sealed record OperationSchemaVariant(string RefId, OpenApiSchema Schema);

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
