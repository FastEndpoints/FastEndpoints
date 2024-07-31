using Newtonsoft.Json.Linq;
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

        if (ctx.Schema.Discriminator is null || ctx.Schema.Example is not null)
            return;

        var jt = ctx.Schema.OneOf.First().ToSampleJson();
        var jo = new JObject { { ctx.Schema.Discriminator, ctx.Schema.DiscriminatorObject.Mapping.First().Key } };

        foreach (var t in jt)
        {
            var p = (JProperty)t;

            if (p.Name != ctx.Schema.Discriminator)
                jo.Add(p);
        }
        ctx.Schema.Example = jo;
    }
}