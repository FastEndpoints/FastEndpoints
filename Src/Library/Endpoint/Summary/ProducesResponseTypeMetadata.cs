using Microsoft.AspNetCore.Http.Metadata;

namespace FastEndpoints;

internal sealed class ProducesResponseTypeMetadata : IProducesResponseTypeMetadata
{
    public Type? Type { get; set; }

    public int StatusCode { get; set; }

    public IEnumerable<string> ContentTypes { get; set; }

    public object? Example { get; set; }
}