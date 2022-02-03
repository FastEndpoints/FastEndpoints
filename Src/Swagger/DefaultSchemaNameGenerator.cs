using NJsonSchema.Generation;

namespace FastEndpoints.Swagger;

internal class DefaultSchemaNameGenerator : ISchemaNameGenerator
{
    private readonly bool shortSchemaNames;

    public DefaultSchemaNameGenerator(bool shortSchemaNames)
    {
        this.shortSchemaNames = shortSchemaNames;
    }

    public string? Generate(Type type)
    {
        if (shortSchemaNames)
            return type.Name;

        return type.FullName?.Replace(".", string.Empty);
    }
}
