using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using static FastEndpoints.OpenApi.OperationReflectionCache;

namespace FastEndpoints.OpenApi;

sealed class ResponseHeaderFactory(DocumentOptions docOpts, SharedContext sharedCtx)
{
    JsonNamingPolicy? NamingPolicy => sharedCtx.NamingPolicy;
    JsonSerializerOptions SerializerOptions => sharedCtx.SerializerOptions ?? Cfg.SerOpts.Options;

    internal void AddTypedHeaders(OpenApiResponse response, Type responseType)
    {
        foreach (var prop in GetPublicInstanceProperties(responseType))
        {
            var toHeaderAttr = GetPropertyMetadata(prop).ToHeader;

            if (toHeaderAttr is null)
                continue;

            var headerName = toHeaderAttr.HeaderName ?? prop.Name.ApplyPropNamingPolicy(docOpts, NamingPolicy);
            var headerType = prop.PropertyType.GetOpenApiParameterType();

            response.AddHeader(
                headerName,
                new()
                {
                    Description = XmlDocLookup.GetPropertySummary(prop),
                    Schema = headerType.GetSchemaForType(sharedCtx, docOpts.ShortSchemaNames),
                    Example = GetHeaderExample(prop, headerType)
                });
        }
    }

    internal void AddConfiguredHeaders(OpenApiResponse response, IEnumerable<ResponseHeader> headers)
    {
        foreach (var header in headers)
        {
            var example = header.Example.JsonNodeFromObject(SerializerOptions);

            response.AddHeader(
                header.HeaderName,
                new()
                {
                    Description = header.Description,
                    Example = example,
                    Schema = CreateConfiguredSchema(header.Example, example)
                });
        }
    }

    JsonNode? GetHeaderExample(PropertyInfo prop, Type headerType)
        => OperationSchemaHelpers.ParseXmlExampleJsonNode(XmlDocLookup.GetPropertyExample(prop), preserveRawString: true) ??
           headerType.GetSampleValue().JsonNodeFromObject(SerializerOptions);

    IOpenApiSchema? CreateConfiguredSchema(object? exampleValue, JsonNode? exampleNode)
    {
        if (exampleValue is null)
            return null;

        var exampleType = exampleValue.GetType();

        if (!IsAnonymousType(exampleType))
            return exampleType.GetSchemaForType(sharedCtx, docOpts.ShortSchemaNames);

        return OperationSchemaHelpers.CreateSchemaFromExampleNode(exampleNode);
    }

    static bool IsAnonymousType(Type type)
        => Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), inherit: false) &&
           type.IsGenericType &&
           type.Name.Contains("AnonymousType", StringComparison.Ordinal) &&
           (type.Name.StartsWith("<>", StringComparison.Ordinal) || type.Name.StartsWith("VB$", StringComparison.Ordinal)) &&
           !type.IsPublic;
}
