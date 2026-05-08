using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.OpenApi;

namespace FastEndpoints.OpenApi;

static class ResponseMetadataCatalog
{
    internal static Dictionary<int, object?> BuildExamples(EndpointDefinition epDef, IList<object> metadata)
    {
        var examples = new Dictionary<int, object?>();

        foreach (var meta in metadata.OfType<DefaultProducesResponseMetadata>())
        {
            if (meta.Example is not null)
                examples[meta.StatusCode] = meta.Example;
        }

        if (epDef.EndpointSummary?.ResponseExamples is { Count: > 0 } explicitExamples)
        {
            foreach (var (statusCode, example) in explicitExamples)
                examples[statusCode] = example;
        }

        return examples;
    }

    internal static Dictionary<int, List<ResponseHeader>> BuildHeadersByStatusCode(IEnumerable<ResponseHeader> headers)
    {
        var result = new Dictionary<int, List<ResponseHeader>>();

        foreach (var header in headers)
        {
            if (!result.TryGetValue(header.StatusCode, out var groupedHeaders))
            {
                groupedHeaders = [];
                result[header.StatusCode] = groupedHeaders;
            }

            groupedHeaders.Add(header);
        }

        return result;
    }

    internal static Dictionary<int, IProducesResponseTypeMetadata> BuildResponseTypeMetadataMap(IList<object> metadata)
    {
        var responseTypeMetas = new Dictionary<int, IProducesResponseTypeMetadata>();

        for (var i = 0; i < metadata.Count; i++)
        {
            if (metadata[i] is IProducesResponseTypeMetadata responseTypeMeta)
                responseTypeMetas[responseTypeMeta.StatusCode] = responseTypeMeta;
        }

        return responseTypeMetas;
    }

    internal static Dictionary<int, Type?> BuildSupportedResponseTypeMap(OpenApiOperationTransformerContext context)
    {
        var responseTypes = context.Description.SupportedResponseTypes;
        var map = new Dictionary<int, Type?>(responseTypes.Count);

        for (var i = 0; i < responseTypes.Count; i++)
        {
            var responseType = responseTypes[i];
            map[responseType.StatusCode] = responseType.Type;
        }

        return map;
    }
}
