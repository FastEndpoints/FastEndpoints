using NJsonSchema;
using NJsonSchema.Generation;

namespace FastEndpoints.Swagger;

internal sealed class MarkNonNullablePropsAsRequired : ISchemaProcessor
{
    public void Process(SchemaProcessorContext context)
    {
        foreach (var (_, prop) in context.Schema.ActualProperties)
        {
            if (!prop.IsNullable(SchemaType.OpenApi3))
                prop.IsRequired = true;
        }
    }
}