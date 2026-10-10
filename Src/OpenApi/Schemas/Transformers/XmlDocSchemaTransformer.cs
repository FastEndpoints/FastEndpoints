using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed class XmlDocSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        var isReference = schema.Metadata?.TryGetValue("x-schema-id", out var schemaId) == true &&
                          schemaId is string { Length: > 0 };

        if (isReference || context.JsonPropertyInfo is null)
        {
            var typeSummary = XmlDocLookup.GetTypeSummary(context.JsonTypeInfo.Type);

            if (typeSummary is not null && string.IsNullOrWhiteSpace(schema.Description))
                schema.Description = typeSummary;
        }

        if (context.JsonPropertyInfo?.AttributeProvider is PropertyInfo propInfo)
        {
            var summary = XmlDocLookup.GetPropertySummary(propInfo);

            if (summary is not null)
            {
                if (isReference)
                    schema.Metadata!["x-ref-description"] = summary;
                else if (string.IsNullOrWhiteSpace(schema.Description))
                    schema.Description = summary;
            }

            if (schema.Example is not null)
                return Task.CompletedTask;

            var example = XmlDocLookup.GetPropertyExample(propInfo);

            if (example is null)
                return Task.CompletedTask;

            schema.Example = OperationSchemaHelpers.ParseXmlExampleJsonNode(example, preserveRawString: true);
        }

        return Task.CompletedTask;
    }
}
