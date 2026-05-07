using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class OpenApiSchemaReferenceCollector
{
    internal static HashSet<string> GetReferencedSchemaRefs(OpenApiDocument document)
    {
        var referencedSchemas = new HashSet<string>(StringComparer.Ordinal);
        var pendingSchemas = new Queue<string>();
        var context = new ReferenceCollectionContext(document, referencedSchemas, pendingSchemas);

        CollectReferencedSchemas(context);

        while (pendingSchemas.Count > 0)
        {
            var refId = pendingSchemas.Dequeue();

            if (document.Components?.Schemas?.TryGetValue(refId, out var s) == true)
                CollectSchemaRefs(s, referencedSchemas, pendingSchemas);
        }

        return referencedSchemas;
    }

    internal static void CollectSchemaRefs(IOpenApiSchema? schema, HashSet<string> refs, Queue<string> pendingRefs)
    {
        switch (schema)
        {
            case null:
                return;
            case OpenApiSchemaReference schemaRef:
            {
                var refId = GetReferenceId(schemaRef);

                if (!string.IsNullOrEmpty(refId) && refs.Add(refId))
                    pendingRefs.Enqueue(refId);

                return;
            }
            case OpenApiSchema s:
            {
                if (s.Properties is { Count: > 0 })
                    CollectSchemaRefs(s.Properties.Values, refs, pendingRefs);

                CollectSchemaRefs(s.Items, refs, pendingRefs);
                CollectSchemaRefs(s.AdditionalProperties, refs, pendingRefs);
                CollectSchemaRefs(s.Not, refs, pendingRefs);

                if (s.AllOf is { Count: > 0 })
                    CollectSchemaRefs(s.AllOf, refs, pendingRefs);

                if (s.OneOf is { Count: > 0 })
                    CollectSchemaRefs(s.OneOf, refs, pendingRefs);

                if (s.AnyOf is { Count: > 0 })
                    CollectSchemaRefs(s.AnyOf, refs, pendingRefs);

                if (s.PatternProperties is { Count: > 0 })
                    CollectSchemaRefs(s.PatternProperties.Values, refs, pendingRefs);

                if (s.Definitions is { Count: > 0 })
                    CollectSchemaRefs(s.Definitions.Values, refs, pendingRefs);

                if (s.Discriminator?.Mapping is { Count: > 0 })
                    CollectSchemaRefs(s.Discriminator.Mapping.Values, refs, pendingRefs);

                break;
            }
        }
    }

    static void CollectReferencedSchemas(ReferenceCollectionContext context)
    {
        if (context.Document.Paths is { Count: > 0 })
        {
            foreach (var pathItem in context.Document.Paths.Values)
                CollectPathItemRefs(pathItem, context);
        }

        if (context.Document.Components is not { } components)
            return;

        if (components.Responses is { Count: > 0 })
        {
            foreach (var (id, response) in components.Responses)
                CollectResponseRefs(response, context, id);
        }

        if (components.Parameters is { Count: > 0 })
        {
            foreach (var (id, parameter) in components.Parameters)
                CollectParameterRefs(parameter, context, id);
        }

        if (components.RequestBodies is { Count: > 0 })
        {
            foreach (var (id, requestBody) in components.RequestBodies)
                CollectRequestBodyRefs(requestBody, context, id);
        }

        if (components.Headers is { Count: > 0 })
        {
            foreach (var (id, header) in components.Headers)
                CollectHeaderRefs(header, context, id);
        }

        if (components.Callbacks is { Count: > 0 })
        {
            foreach (var (id, callback) in components.Callbacks)
                CollectCallbackRefs(callback, context, id);
        }

        if (components.PathItems is { Count: > 0 })
        {
            foreach (var pathItem in components.PathItems.Values)
                CollectPathItemRefs(pathItem, context);
        }
    }

    static void CollectPathItemRefs(IOpenApiPathItem? pathItem, ReferenceCollectionContext context)
    {
        if (pathItem?.Parameters is { Count: > 0 })
        {
            foreach (var parameter in pathItem.Parameters)
                CollectParameterRefs(parameter, context);
        }

        if (pathItem?.Operations is not { Count: > 0 })
            return;

        foreach (var op in pathItem.Operations.Values)
        {
            if (op.Parameters is { Count: > 0 })
            {
                foreach (var parameter in op.Parameters)
                    CollectParameterRefs(parameter, context);
            }

            CollectRequestBodyRefs(op.RequestBody, context);

            if (op.Responses is { Count: > 0 })
            {
                foreach (var resp in op.Responses.Values)
                    CollectResponseRefs(resp, context);
            }

            if (op.Callbacks is { Count: > 0 })
            {
                foreach (var callback in op.Callbacks.Values)
                    CollectCallbackRefs(callback, context);
            }
        }
    }

    static void CollectResponseRefs(IOpenApiResponse? response, ReferenceCollectionContext context, string? componentId = null)
    {
        if (response is null)
            return;

        if (componentId is not null && !context.WalkedResponses.Add(componentId))
            return;

        if (response is OpenApiResponseReference responseRef &&
            TryCollectReferencedComponent(responseRef.Reference, context.Document.Components?.Responses, context))
            return;

        if (response.Headers is { Count: > 0 })
        {
            foreach (var header in response.Headers.Values)
                CollectHeaderRefs(header, context);
        }

        if (response.Content is { Count: > 0 })
        {
            foreach (var mediaType in response.Content.Values)
                CollectMediaTypeRefs(mediaType, context);
        }
    }

    static void CollectParameterRefs(IOpenApiParameter? parameter, ReferenceCollectionContext context, string? componentId = null)
    {
        if (parameter is null)
            return;

        if (componentId is not null && !context.WalkedParameters.Add(componentId))
            return;

        if (parameter is OpenApiParameterReference parameterRef &&
            TryCollectReferencedComponent(parameterRef.Reference, context.Document.Components?.Parameters, context))
            return;

        context.CollectSchemaRefs(parameter.Schema);

        if (parameter.Content is { Count: > 0 })
        {
            foreach (var mediaType in parameter.Content.Values)
                CollectMediaTypeRefs(mediaType, context);
        }
    }

    static void CollectRequestBodyRefs(IOpenApiRequestBody? requestBody, ReferenceCollectionContext context, string? componentId = null)
    {
        if (requestBody is null)
            return;

        if (componentId is not null && !context.WalkedRequestBodies.Add(componentId))
            return;

        if (requestBody is OpenApiRequestBodyReference requestBodyRef &&
            TryCollectReferencedComponent(requestBodyRef.Reference, context.Document.Components?.RequestBodies, context))
            return;

        if (requestBody.Content is { Count: > 0 })
        {
            foreach (var mediaType in requestBody.Content.Values)
                CollectMediaTypeRefs(mediaType, context);
        }
    }

    static void CollectHeaderRefs(IOpenApiHeader? header, ReferenceCollectionContext context, string? componentId = null)
    {
        if (header is null)
            return;

        if (componentId is not null && !context.WalkedHeaders.Add(componentId))
            return;

        if (header is OpenApiHeaderReference headerRef &&
            TryCollectReferencedComponent(headerRef.Reference, context.Document.Components?.Headers, context))
            return;

        context.CollectSchemaRefs(header.Schema);

        if (header.Content is { Count: > 0 })
        {
            foreach (var mediaType in header.Content.Values)
                CollectMediaTypeRefs(mediaType, context);
        }
    }

    static void CollectCallbackRefs(IOpenApiCallback? callback, ReferenceCollectionContext context, string? componentId = null)
    {
        if (callback is null)
            return;

        if (componentId is not null && !context.WalkedCallbacks.Add(componentId))
            return;

        if (callback is OpenApiCallbackReference callbackRef &&
            TryCollectReferencedCallback(callbackRef.Reference, context))
            return;

        if (callback.PathItems is not { Count: > 0 })
            return;

        foreach (var pathItem in callback.PathItems.Values)
            CollectPathItemRefs(pathItem, context);
    }

    static void CollectMediaTypeRefs(OpenApiMediaType? mediaType, ReferenceCollectionContext context)
    {
        if (mediaType is null)
            return;

        context.CollectSchemaRefs(mediaType.Schema);

        if (mediaType.Encoding is { Count: > 0 })
        {
            foreach (var encoding in mediaType.Encoding.Values)
                CollectEncodingRefs(encoding, context);
        }
    }

    static void CollectEncodingRefs(OpenApiEncoding? encoding, ReferenceCollectionContext context)
    {
        if (encoding?.Headers is { Count: > 0 })
        {
            foreach (var header in encoding.Headers.Values)
                CollectHeaderRefs(header, context);
        }
    }

    static bool TryCollectReferencedComponent<TComponent>(BaseOpenApiReference reference,
                                                          IDictionary<string, TComponent>? components,
                                                          ReferenceCollectionContext context)
        where TComponent : class
    {
        var id = reference.Id;

        if (string.IsNullOrEmpty(id) || reference.IsExternal || components?.TryGetValue(id, out var component) != true)
            return false;

        switch (component)
        {
            case IOpenApiResponse response:
                CollectResponseRefs(response, context, id);

                break;
            case IOpenApiParameter parameter:
                CollectParameterRefs(parameter, context, id);

                break;
            case IOpenApiRequestBody requestBody:
                CollectRequestBodyRefs(requestBody, context, id);

                break;
            case IOpenApiHeader header:
                CollectHeaderRefs(header, context, id);

                break;
            default:
                return false;
        }

        return true;
    }

    static bool TryCollectReferencedCallback(BaseOpenApiReference reference, ReferenceCollectionContext context)
    {
        var id = reference.Id;

        if (string.IsNullOrEmpty(id) || reference.IsExternal || context.Document.Components?.Callbacks?.TryGetValue(id, out var callback) != true)
            return false;

        CollectCallbackRefs(callback, context, id);

        return true;
    }

    static void CollectSchemaRefs(IEnumerable<IOpenApiSchema?> schemas, HashSet<string> refs, Queue<string> pendingRefs)
    {
        foreach (var schema in schemas)
            CollectSchemaRefs(schema, refs, pendingRefs);
    }

    static string? GetReferenceId(OpenApiSchemaReference schemaRef)
        => schemaRef.Reference.Id ?? schemaRef.Id;

    sealed class ReferenceCollectionContext(OpenApiDocument document, HashSet<string> refs, Queue<string> pendingRefs)
    {
        public OpenApiDocument Document { get; } = document;
        public HashSet<string> WalkedResponses { get; } = new(StringComparer.Ordinal);
        public HashSet<string> WalkedParameters { get; } = new(StringComparer.Ordinal);
        public HashSet<string> WalkedRequestBodies { get; } = new(StringComparer.Ordinal);
        public HashSet<string> WalkedHeaders { get; } = new(StringComparer.Ordinal);
        public HashSet<string> WalkedCallbacks { get; } = new(StringComparer.Ordinal);

        public void CollectSchemaRefs(IOpenApiSchema? schema)
            => OpenApiSchemaReferenceCollector.CollectSchemaRefs(schema, refs, pendingRefs);
    }
}
