using Microsoft.AspNetCore.Http.Metadata;

#pragma warning disable CS8618

namespace FastEndpoints;

sealed class DefaultProducesResponseMetadata(Type type, int statusCode, IEnumerable<string> contentTypes) : IProducesResponseTypeMetadata
{
    public Type? Type { get; } = type;

    public int StatusCode { get; } = statusCode;

    public IEnumerable<string> ContentTypes { get; } = contentTypes;

    public string? Description { get; set; }

    public object? Example { get; set; }
}