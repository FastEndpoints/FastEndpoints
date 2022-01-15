using NJsonSchema.Generation;

namespace FastEndpoints.NSwag;

internal class DefaultSchemaNameGenerator : ISchemaNameGenerator
{
    public string? Generate(Type type) => type.FullName?.Replace(".", "_");
}
