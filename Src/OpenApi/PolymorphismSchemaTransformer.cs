using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

/// <summary>
/// schema transformer that populates oneOf from discriminator mappings when UseOneOfForPolymorphism is enabled.
/// </summary>
sealed class PolymorphismSchemaTransformer(DocumentOptions opts) : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (!opts.UseOneOfForPolymorphism)
            return Task.CompletedTask;

        // only process schemas that have discriminator mappings but no oneOf entries yet
        if (schema.Discriminator?.Mapping is not { Count: > 0 } ||
            schema.OneOf is { Count: > 0 })
            return Task.CompletedTask;

        // populate oneOf from discriminator mapping values (which are schema references)
        schema.OneOf ??= [];

        foreach (var (_, derivedSchemaRef) in schema.Discriminator.Mapping)
            schema.OneOf.Add(derivedSchemaRef);

        // generate example from first derived type if discriminator property name is set and no example exists
        if (schema.Discriminator.PropertyName is not null && schema.Example is null && schema.OneOf is { Count: > 0 })
        {
            var firstMapping = schema.Discriminator.Mapping.First();

            try
            {
                var exampleObj = new System.Text.Json.Nodes.JsonObject
                {
                    [schema.Discriminator.PropertyName] = firstMapping.Key
                };
                schema.Example = exampleObj;
            }
            catch
            {
                // ignore example generation failures
            }
        }

        return Task.CompletedTask;
    }
}