using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

/// <summary>
/// schema transformer that records a discriminator example when UseOneOfForPolymorphism is enabled.
/// oneOf is applied later by <see cref="DocumentPolymorphicOneOf"/> once mapping refs can be bound to the document.
/// </summary>
sealed class PolymorphismSchemaTransformer(DocumentOptions opts) : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (!opts.UseOneOfForPolymorphism)
            return Task.CompletedTask;

        // mapping refs are built with a null host document. copying them into oneOf here makes schema resolution throw.
        // DocumentPolymorphicOneOf binds oneOf after components exist.
        if (schema.Discriminator?.Mapping is not { Count: > 0 } ||
            schema.OneOf is { Count: > 0 })
            return Task.CompletedTask;

        // generate example from first derived type if discriminator property name is set and no example exists
        if (schema.Discriminator.PropertyName is not null && schema.Example is null)
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
