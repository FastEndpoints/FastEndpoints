using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed class XmlDocSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        if (context.JsonPropertyInfo is not null)
        {
            if (context.JsonPropertyInfo.AttributeProvider is PropertyInfo propInfo)
            {
                var summary = XmlDocLookup.GetPropertySummary(propInfo);

                if (summary is not null && string.IsNullOrWhiteSpace(schema.Description))
                    schema.Description = summary;

                if (schema.Example is not null)
                    return Task.CompletedTask;

                var example = XmlDocLookup.GetPropertyExample(propInfo);

                if (example is null)
                    return Task.CompletedTask;

                schema.Example = OperationSchemaHelpers.ParseXmlExampleJsonNode(example, preserveRawString: true);
            }
        }
        else
        {
            var summary = XmlDocLookup.GetTypeSummary(context.JsonTypeInfo.Type);

            if (summary is not null && string.IsNullOrWhiteSpace(schema.Description))
                schema.Description = summary;
        }

        return Task.CompletedTask;
    }
}
