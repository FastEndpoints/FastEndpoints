using System.Collections.Concurrent;
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

    internal JsonNamingPolicy? ResolveNamingPolicy(IServiceProvider services)
        => NamingPolicy ??= services.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions.PropertyNamingPolicy;

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
