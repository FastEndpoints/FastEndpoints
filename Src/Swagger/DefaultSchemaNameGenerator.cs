using NJsonSchema.Generation;

namespace FastEndpoints.Swagger;

internal class DefaultSchemaNameGenerator : ISchemaNameGenerator
{
    public string? Generate(Type type) => type.FullName?.Replace(".", string.Empty);
}
