using Microsoft.AspNetCore.Http.Metadata;

#pragma warning disable CS8618

namespace FastEndpoints;

sealed class ProducesResponseTypeMetadata : IProducesResponseTypeMetadata
{
    public Type? Type { get; set; }

    public int StatusCode { get; set; }

    public string? Description { get; set; }

    public IEnumerable<string> ContentTypes { get; set; }

    public object? Example { get; set; }
}