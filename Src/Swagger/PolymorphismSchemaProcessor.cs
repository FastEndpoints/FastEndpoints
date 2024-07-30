using NJsonSchema.Generation;

namespace FastEndpoints.Swagger;

sealed class PolymorphismSchemaProcessor(DocumentOptions opts) : ISchemaProcessor
{
    public void Process(SchemaProcessorContext ctx)
    {
        if (opts.UseOneOfForPolymorphism is false ||
            ctx.Schema.DiscriminatorObject?.Mapping.Count is null or 0 ||
            ctx.Schema.OneOf.Count != 0)
            return;

        foreach (var derSchema in ctx.Schema.DiscriminatorObject.Mapping.Values)
            ctx.Schema.OneOf.Add(derSchema);
    }
}