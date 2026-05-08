using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class OpenApiSchemaGraphTransformer
{
    internal static void TransformDocumentSchemas(OpenApiDocument document, Func<IOpenApiSchema?, IOpenApiSchema?> transform)
    {
        if (document.Paths is { Count: > 0 })
        {
            foreach (var pathItem in document.Paths.Values)
                RewritePathItemSchemas(pathItem, transform);
        }

        if (document.Components is not { } components)
            return;

        if (components.Schemas is { Count: > 0 })
        {
            foreach (var (key, schema) in components.Schemas.ToArray())
                components.Schemas[key] = OpenApiSchemaTraversal.Rewrite(schema, transform)!;
        }

        if (components.Responses is { Count: > 0 })
        {
            foreach (var response in components.Responses.Values)
                RewriteResponseSchemas(response, transform);
        }

        if (components.Parameters is { Count: > 0 })
        {
            foreach (var parameter in components.Parameters.Values)
                RewriteParameterSchemas(parameter, transform);
        }

        if (components.RequestBodies is { Count: > 0 })
        {
            foreach (var requestBody in components.RequestBodies.Values)
                RewriteRequestBodySchemas(requestBody, transform);
        }

        if (components.Headers is { Count: > 0 })
        {
            foreach (var header in components.Headers.Values)
                RewriteHeaderSchemas(header, transform);
        }

        if (components.Callbacks is { Count: > 0 })
        {
            foreach (var callback in components.Callbacks.Values)
                RewriteCallbackSchemas(callback, transform);
        }

        if (components.PathItems is { Count: > 0 })
        {
            foreach (var pathItem in components.PathItems.Values)
                RewritePathItemSchemas(pathItem, transform);
        }
    }

    static void RewritePathItemSchemas(IOpenApiPathItem? pathItem, Func<IOpenApiSchema?, IOpenApiSchema?> rewrite)
    {
        if (pathItem?.Parameters is { Count: > 0 })
        {
            foreach (var parameter in pathItem.Parameters)
                RewriteParameterSchemas(parameter, rewrite);
        }

        if (pathItem?.Operations is not { Count: > 0 })
            return;

        foreach (var operation in pathItem.Operations.Values)
        {
            if (operation.Parameters is { Count: > 0 })
            {
                foreach (var parameter in operation.Parameters)
                    RewriteParameterSchemas(parameter, rewrite);
            }

            RewriteRequestBodySchemas(operation.RequestBody, rewrite);

            if (operation.Responses is { Count: > 0 })
            {
                foreach (var response in operation.Responses.Values)
                    RewriteResponseSchemas(response, rewrite);
            }

            if (operation.Callbacks is { Count: > 0 })
            {
                foreach (var callback in operation.Callbacks.Values)
                    RewriteCallbackSchemas(callback, rewrite);
            }
        }
    }

    static void RewriteResponseSchemas(IOpenApiResponse? response, Func<IOpenApiSchema?, IOpenApiSchema?> rewrite)
    {
        if (response is null)
            return;

        if (response.Headers is { Count: > 0 })
        {
            foreach (var header in response.Headers.Values)
                RewriteHeaderSchemas(header, rewrite);
        }

        if (response.Content is { Count: > 0 })
        {
            foreach (var mediaType in response.Content.Values)
                RewriteMediaTypeSchemas(mediaType, rewrite);
        }
    }

    static void RewriteParameterSchemas(IOpenApiParameter? parameter, Func<IOpenApiSchema?, IOpenApiSchema?> rewrite)
    {
        if (parameter is null)
            return;

        if (parameter is OpenApiParameter concreteParameter)
            concreteParameter.Schema = OpenApiSchemaTraversal.Rewrite(parameter.Schema, rewrite);

        if (parameter.Content is { Count: > 0 })
        {
            foreach (var mediaType in parameter.Content.Values)
                RewriteMediaTypeSchemas(mediaType, rewrite);
        }
    }

    static void RewriteRequestBodySchemas(IOpenApiRequestBody? requestBody, Func<IOpenApiSchema?, IOpenApiSchema?> rewrite)
    {
        if (requestBody?.Content is not { Count: > 0 })
            return;

        foreach (var mediaType in requestBody.Content.Values)
            RewriteMediaTypeSchemas(mediaType, rewrite);
    }

    static void RewriteHeaderSchemas(IOpenApiHeader? header, Func<IOpenApiSchema?, IOpenApiSchema?> rewrite)
    {
        if (header is null)
            return;

        if (header is OpenApiHeader concreteHeader)
            concreteHeader.Schema = OpenApiSchemaTraversal.Rewrite(header.Schema, rewrite);

        if (header.Content is { Count: > 0 })
        {
            foreach (var mediaType in header.Content.Values)
                RewriteMediaTypeSchemas(mediaType, rewrite);
        }
    }

    static void RewriteCallbackSchemas(IOpenApiCallback? callback, Func<IOpenApiSchema?, IOpenApiSchema?> rewrite)
    {
        if (callback?.PathItems is not { Count: > 0 })
            return;

        foreach (var pathItem in callback.PathItems.Values)
            RewritePathItemSchemas(pathItem, rewrite);
    }

    static void RewriteMediaTypeSchemas(OpenApiMediaType? mediaType, Func<IOpenApiSchema?, IOpenApiSchema?> rewrite)
    {
        if (mediaType is null)
            return;

        mediaType.Schema = OpenApiSchemaTraversal.Rewrite(mediaType.Schema, rewrite);

        if (mediaType.Encoding is { Count: > 0 })
        {
            foreach (var encoding in mediaType.Encoding.Values)
                RewriteEncodingSchemas(encoding, rewrite);
        }
    }

    static void RewriteEncodingSchemas(OpenApiEncoding? encoding, Func<IOpenApiSchema?, IOpenApiSchema?> rewrite)
    {
        if (encoding?.Headers is not { Count: > 0 })
            return;

        foreach (var header in encoding.Headers.Values)
            RewriteHeaderSchemas(header, rewrite);
    }

}
