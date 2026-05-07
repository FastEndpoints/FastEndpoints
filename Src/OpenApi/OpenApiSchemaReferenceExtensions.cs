using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class OpenApiSchemaReferenceExtensions
{
    internal static string? GetReferenceId(this OpenApiSchemaReference schemaRef)
        => schemaRef.Reference.Id ?? schemaRef.Id;
}
