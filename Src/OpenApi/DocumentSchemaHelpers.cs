using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class DocumentSchemaHelpers
{
    extension(OpenApiDocument document)
    {
        internal void RemoveUnreferencedSchemas()
        {
            if (document.Components?.Schemas is not { Count: > 0 } schemas || document.Paths is not { Count: > 0 })
                return;

            var referencedSchemas = document.GetReferencedSchemaRefs();

            foreach (var key in schemas.Keys.ToArray())
            {
                if (!referencedSchemas.Contains(key))
                    schemas.Remove(key);
            }
        }

        internal void RemovePromotedRequestWrapperSchemas(SharedContext sharedCtx)
        {
            if (sharedCtx.PromotedRequestWrapperSchemaRefs.IsEmpty || document.Components?.Schemas is not { Count: > 0 } schemas)
                return;

            var referencedSchemas = document.GetReferencedSchemaRefs();

            foreach (var refId in sharedCtx.PromotedRequestWrapperSchemaRefs.Keys)
            {
                if (!referencedSchemas.Contains(refId))
                    schemas.Remove(refId);
            }
        }

        internal void RemoveFormFileSchemas()
        {
            if (document.Components?.Schemas is { Count: > 0 })
            {
                foreach (var (_, iSchema) in document.Components.Schemas)
                {
                    if (iSchema is OpenApiSchema schema)
                        InlineFormFileRefs(schema);
                }
            }

            if (document.Paths is { Count: > 0 })
            {
                foreach (var pathItem in document.Paths.Values)
                {
                    if (pathItem.Operations is null)
                        continue;

                    foreach (var op in pathItem.Operations.Values)
                    {
                        if (op.RequestBody?.Content is { Count: > 0 })
                        {
                            foreach (var content in op.RequestBody.Content.Values)
                                RewriteFormFileMediaType(content);
                        }

                        if (op.Responses is { Count: > 0 })
                        {
                            foreach (var resp in op.Responses.Values)
                            {
                                if (resp is OpenApiResponse { Content.Count: > 0 } concreteResp)
                                {
                                    foreach (var content in concreteResp.Content.Values)
                                        RewriteFormFileMediaType(content);
                                }
                            }
                        }
                    }
                }
            }

            if (document.Components?.Schemas is null)
                return;

            foreach (var key in document.Components.Schemas.Keys.ToArray())
            {
                if (key.Contains("IFormFile", StringComparison.Ordinal))
                    document.Components.Schemas.Remove(key);
            }
        }

        internal async Task AddMissingSchemas(SharedContext sharedCtx, OpenApiDocumentTransformerContext context, CancellationToken ct)
        {
            if (sharedCtx.MissingSchemaTypes.IsEmpty)
                return;

            document.Components ??= new();
            document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

            foreach (var (refId, type) in sharedCtx.MissingSchemaTypes)
            {
                if (document.Components.Schemas.ContainsKey(refId))
                    continue;

                var schema = await context.GetOrCreateSchemaAsync(type, parameterDescription: null, ct);

                if (schema is not null)
                    document.Components.Schemas.TryAdd(refId, schema);
            }
        }

        internal HashSet<string> GetReferencedSchemaRefs()
        {
            var referencedSchemas = new HashSet<string>(StringComparer.Ordinal);
            var pendingSchemas = new Queue<string>();
            CollectReferencedSchemas(document, referencedSchemas, pendingSchemas);

            while (pendingSchemas.Count > 0)
            {
                var refId = pendingSchemas.Dequeue();

                if (document.Components?.Schemas?.TryGetValue(refId, out var s) == true)
                    CollectSchemaRefs(s, referencedSchemas, pendingSchemas);
            }

            return referencedSchemas;
        }

    }

    internal static void SortPaths(this OpenApiDocument document)
    {
        var sorted = document.Paths.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
        document.Paths.Clear();

        foreach (var (path, pathItem) in sorted)
            document.Paths[path] = pathItem;
    }

    internal static void SortResponses(this OpenApiDocument document)
    {
        foreach (var (_, pathItem) in document.Paths)
        {
            if (pathItem.Operations is null)
                continue;

            foreach (var (_, operation) in pathItem.Operations)
            {
                if (operation.Responses is not { Count: > 1 })
                    continue;

                var sorted = operation.Responses.OrderBy(r => r.Key, StringComparer.Ordinal).ToList();
                operation.Responses.Clear();

                foreach (var (key, value) in sorted)
                    operation.Responses[key] = value;
            }
        }
    }

    internal static void SortSchemas(this OpenApiDocument document)
    {
        if (document.Components?.Schemas is null)
            return;

        var sorted = document.Components.Schemas.OrderBy(s => s.Key, StringComparer.Ordinal).ToList();
        document.Components.Schemas.Clear();

        foreach (var (key, schema) in sorted)
            document.Components.Schemas[key] = schema;
    }

    static void CollectReferencedSchemas(OpenApiDocument document, HashSet<string> refs, Queue<string> pendingRefs)
    {
        var walkedResponses = new HashSet<string>(StringComparer.Ordinal);
        var walkedParameters = new HashSet<string>(StringComparer.Ordinal);
        var walkedRequestBodies = new HashSet<string>(StringComparer.Ordinal);
        var walkedHeaders = new HashSet<string>(StringComparer.Ordinal);
        var walkedCallbacks = new HashSet<string>(StringComparer.Ordinal);

        if (document.Paths is { Count: > 0 })
        {
            foreach (var pathItem in document.Paths.Values)
                CollectPathItemRefs(pathItem, document, refs, pendingRefs, walkedResponses, walkedParameters, walkedRequestBodies, walkedHeaders, walkedCallbacks);
        }

        if (document.Components is not { } components)
            return;

        if (components.Responses is { Count: > 0 })
        {
            foreach (var (id, response) in components.Responses)
                CollectResponseRefs(response, document, refs, pendingRefs, walkedResponses, walkedHeaders, id);
        }

        if (components.Parameters is { Count: > 0 })
        {
            foreach (var (id, parameter) in components.Parameters)
                CollectParameterRefs(parameter, document, refs, pendingRefs, walkedParameters, id);
        }

        if (components.RequestBodies is { Count: > 0 })
        {
            foreach (var (id, requestBody) in components.RequestBodies)
                CollectRequestBodyRefs(requestBody, document, refs, pendingRefs, walkedRequestBodies, id);
        }

        if (components.Headers is { Count: > 0 })
        {
            foreach (var (id, header) in components.Headers)
                CollectHeaderRefs(header, document, refs, pendingRefs, walkedHeaders, id);
        }

        if (components.Callbacks is { Count: > 0 })
        {
            foreach (var (id, callback) in components.Callbacks)
                CollectCallbackRefs(callback, document, refs, pendingRefs, walkedResponses, walkedParameters, walkedRequestBodies, walkedHeaders, walkedCallbacks, id);
        }

        if (components.PathItems is { Count: > 0 })
        {
            foreach (var pathItem in components.PathItems.Values)
                CollectPathItemRefs(pathItem, document, refs, pendingRefs, walkedResponses, walkedParameters, walkedRequestBodies, walkedHeaders, walkedCallbacks);
        }
    }

    static void CollectPathItemRefs(IOpenApiPathItem? pathItem,
                                    OpenApiDocument document,
                                    HashSet<string> refs,
                                    Queue<string> pendingRefs,
                                    HashSet<string> walkedResponses,
                                    HashSet<string> walkedParameters,
                                    HashSet<string> walkedRequestBodies,
                                    HashSet<string> walkedHeaders,
                                    HashSet<string> walkedCallbacks)
    {
        if (pathItem?.Parameters is { Count: > 0 })
        {
            foreach (var parameter in pathItem.Parameters)
                CollectParameterRefs(parameter, document, refs, pendingRefs, walkedParameters);
        }

        if (pathItem?.Operations is not { Count: > 0 })
            return;

        foreach (var op in pathItem.Operations.Values)
        {
            if (op.Parameters is { Count: > 0 })
            {
                foreach (var parameter in op.Parameters)
                    CollectParameterRefs(parameter, document, refs, pendingRefs, walkedParameters);
            }

            CollectRequestBodyRefs(op.RequestBody, document, refs, pendingRefs, walkedRequestBodies);

            if (op.Responses is { Count: > 0 })
            {
                foreach (var resp in op.Responses.Values)
                    CollectResponseRefs(resp, document, refs, pendingRefs, walkedResponses, walkedHeaders);
            }

            if (op.Callbacks is { Count: > 0 })
            {
                foreach (var callback in op.Callbacks.Values)
                    CollectCallbackRefs(callback, document, refs, pendingRefs, walkedResponses, walkedParameters, walkedRequestBodies, walkedHeaders, walkedCallbacks);
            }
        }
    }

    static void CollectResponseRefs(IOpenApiResponse? response,
                                    OpenApiDocument document,
                                    HashSet<string> refs,
                                    Queue<string> pendingRefs,
                                    HashSet<string> walkedResponses,
                                    HashSet<string> walkedHeaders,
                                    string? componentId = null)
    {
        if (response is null)
            return;

        if (componentId is not null && !walkedResponses.Add(componentId))
            return;

        if (response is OpenApiResponseReference responseRef && TryCollectReferencedComponent(responseRef.Reference, document.Components?.Responses, document, refs, pendingRefs, walkedResponses, walkedHeaders))
            return;

        if (response.Headers is { Count: > 0 })
        {
            foreach (var header in response.Headers.Values)
                CollectHeaderRefs(header, document, refs, pendingRefs, walkedHeaders);
        }

        if (response.Content is { Count: > 0 })
        {
            foreach (var mediaType in response.Content.Values)
                CollectMediaTypeRefs(mediaType, document, refs, pendingRefs, walkedHeaders);
        }
    }

    static void CollectParameterRefs(IOpenApiParameter? parameter,
                                     OpenApiDocument document,
                                     HashSet<string> refs,
                                     Queue<string> pendingRefs,
                                     HashSet<string> walkedParameters,
                                     string? componentId = null)
    {
        if (parameter is null)
            return;

        if (componentId is not null && !walkedParameters.Add(componentId))
            return;

        if (parameter is OpenApiParameterReference parameterRef && TryCollectReferencedComponent(parameterRef.Reference, document.Components?.Parameters, document, refs, pendingRefs, walkedParameters))
            return;

        CollectSchemaRefs(parameter.Schema, refs, pendingRefs);

        if (parameter.Content is { Count: > 0 })
        {
            foreach (var mediaType in parameter.Content.Values)
                CollectMediaTypeRefs(mediaType, document, refs, pendingRefs, new(StringComparer.Ordinal));
        }
    }

    static void CollectRequestBodyRefs(IOpenApiRequestBody? requestBody,
                                       OpenApiDocument document,
                                       HashSet<string> refs,
                                       Queue<string> pendingRefs,
                                       HashSet<string> walkedRequestBodies,
                                       string? componentId = null)
    {
        if (requestBody is null)
            return;

        if (componentId is not null && !walkedRequestBodies.Add(componentId))
            return;

        if (requestBody is OpenApiRequestBodyReference requestBodyRef && TryCollectReferencedComponent(requestBodyRef.Reference, document.Components?.RequestBodies, document, refs, pendingRefs, walkedRequestBodies))
            return;

        if (requestBody.Content is { Count: > 0 })
        {
            foreach (var mediaType in requestBody.Content.Values)
                CollectMediaTypeRefs(mediaType, document, refs, pendingRefs, new(StringComparer.Ordinal));
        }
    }

    static void CollectHeaderRefs(IOpenApiHeader? header,
                                  OpenApiDocument document,
                                  HashSet<string> refs,
                                  Queue<string> pendingRefs,
                                  HashSet<string> walkedHeaders,
                                  string? componentId = null)
    {
        if (header is null)
            return;

        if (componentId is not null && !walkedHeaders.Add(componentId))
            return;

        if (header is OpenApiHeaderReference headerRef && TryCollectReferencedComponent(headerRef.Reference, document.Components?.Headers, document, refs, pendingRefs, walkedHeaders))
            return;

        CollectSchemaRefs(header.Schema, refs, pendingRefs);

        if (header.Content is { Count: > 0 })
        {
            foreach (var mediaType in header.Content.Values)
                CollectMediaTypeRefs(mediaType, document, refs, pendingRefs, walkedHeaders);
        }
    }

    static void CollectCallbackRefs(IOpenApiCallback? callback,
                                    OpenApiDocument document,
                                    HashSet<string> refs,
                                    Queue<string> pendingRefs,
                                    HashSet<string> walkedResponses,
                                    HashSet<string> walkedParameters,
                                    HashSet<string> walkedRequestBodies,
                                    HashSet<string> walkedHeaders,
                                    HashSet<string> walkedCallbacks,
                                    string? componentId = null)
    {
        if (callback is null)
            return;

        if (componentId is not null && !walkedCallbacks.Add(componentId))
            return;

        if (callback is OpenApiCallbackReference callbackRef &&
            TryCollectReferencedCallback(callbackRef.Reference, document, refs, pendingRefs, walkedResponses, walkedParameters, walkedRequestBodies, walkedHeaders, walkedCallbacks))
            return;

        if (callback.PathItems is not { Count: > 0 })
            return;

        foreach (var pathItem in callback.PathItems.Values)
            CollectPathItemRefs(pathItem, document, refs, pendingRefs, walkedResponses, walkedParameters, walkedRequestBodies, walkedHeaders, walkedCallbacks);
    }

    static void CollectMediaTypeRefs(OpenApiMediaType? mediaType,
                                     OpenApiDocument document,
                                     HashSet<string> refs,
                                     Queue<string> pendingRefs,
                                     HashSet<string> walkedHeaders)
    {
        if (mediaType is null)
            return;

        CollectSchemaRefs(mediaType.Schema, refs, pendingRefs);

        if (mediaType.Encoding is { Count: > 0 })
        {
            foreach (var encoding in mediaType.Encoding.Values)
                CollectEncodingRefs(encoding, document, refs, pendingRefs, walkedHeaders);
        }
    }

    static void CollectEncodingRefs(OpenApiEncoding? encoding,
                                    OpenApiDocument document,
                                    HashSet<string> refs,
                                    Queue<string> pendingRefs,
                                    HashSet<string> walkedHeaders)
    {
        if (encoding?.Headers is { Count: > 0 })
        {
            foreach (var header in encoding.Headers.Values)
                CollectHeaderRefs(header, document, refs, pendingRefs, walkedHeaders);
        }
    }

    static bool TryCollectReferencedComponent<TComponent>(BaseOpenApiReference reference,
                                                          IDictionary<string, TComponent>? components,
                                                          OpenApiDocument document,
                                                          HashSet<string> refs,
                                                          Queue<string> pendingRefs,
                                                          HashSet<string> walkedRefs,
                                                          HashSet<string>? walkedHeaders = null)
        where TComponent : class
    {
        var id = reference.Id;

        if (string.IsNullOrEmpty(id) || reference.IsExternal || components?.TryGetValue(id, out var component) != true)
            return false;

        switch (component)
        {
            case IOpenApiResponse response:
                CollectResponseRefs(response, document, refs, pendingRefs, walkedRefs, walkedHeaders ?? new(StringComparer.Ordinal), id);
                break;
            case IOpenApiParameter parameter:
                CollectParameterRefs(parameter, document, refs, pendingRefs, walkedRefs, id);
                break;
            case IOpenApiRequestBody requestBody:
                CollectRequestBodyRefs(requestBody, document, refs, pendingRefs, walkedRefs, id);
                break;
            case IOpenApiHeader header:
                CollectHeaderRefs(header, document, refs, pendingRefs, walkedRefs, id);
                break;
            default:
                return false;
        }

        return true;
    }

    static bool TryCollectReferencedCallback(BaseOpenApiReference reference,
                                             OpenApiDocument document,
                                             HashSet<string> refs,
                                             Queue<string> pendingRefs,
                                             HashSet<string> walkedResponses,
                                             HashSet<string> walkedParameters,
                                             HashSet<string> walkedRequestBodies,
                                             HashSet<string> walkedHeaders,
                                             HashSet<string> walkedCallbacks)
    {
        var id = reference.Id;

        if (string.IsNullOrEmpty(id) || reference.IsExternal || document.Components?.Callbacks?.TryGetValue(id, out var callback) != true)
            return false;

        CollectCallbackRefs(callback, document, refs, pendingRefs, walkedResponses, walkedParameters, walkedRequestBodies, walkedHeaders, walkedCallbacks, id);

        return true;
    }

    static void CollectSchemaRefs(IEnumerable<IOpenApiSchema?> schemas, HashSet<string> refs, Queue<string> pendingRefs)
    {
        foreach (var schema in schemas)
            CollectSchemaRefs(schema, refs, pendingRefs);
    }

    static void CollectSchemaRefs(IOpenApiSchema? schema, HashSet<string> refs, Queue<string> pendingRefs)
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

                CollectSchemaRefs([s.Items, s.AdditionalProperties], refs, pendingRefs);

                if (s.AllOf is { Count: > 0 })
                    CollectSchemaRefs(s.AllOf, refs, pendingRefs);

                if (s.OneOf is { Count: > 0 })
                    CollectSchemaRefs(s.OneOf, refs, pendingRefs);

                if (s.AnyOf is { Count: > 0 })
                    CollectSchemaRefs(s.AnyOf, refs, pendingRefs);

                if (s.Discriminator?.Mapping is { Count: > 0 })
                    CollectSchemaRefs(s.Discriminator.Mapping.Values, refs, pendingRefs);

                break;
            }
        }
    }

    static void InlineFormFileRefs(OpenApiSchema schema)
    {
        if (schema.Properties is { Count: > 0 })
        {
            foreach (var propName in schema.Properties.Keys.ToArray())
            {
                var propSchema = schema.Properties[propName];
                if (RewriteFormFileSchema(propSchema) is { } rewrittenSchema)
                    schema.Properties[propName] = rewrittenSchema;
            }
        }

        schema.Items = RewriteFormFileSchema(schema.Items);
        schema.AdditionalProperties = RewriteFormFileSchema(schema.AdditionalProperties);

        if (schema.AllOf is { Count: > 0 })
            RewriteFormFileSchemaList(schema.AllOf);

        if (schema.OneOf is { Count: > 0 })
            RewriteFormFileSchemaList(schema.OneOf);

        if (schema.AnyOf is { Count: > 0 })
            RewriteFormFileSchemaList(schema.AnyOf);
    }

    static void RewriteFormFileMediaType(OpenApiMediaType content)
    {
        content.Schema = RewriteFormFileSchema(content.Schema);
    }

    static IOpenApiSchema? RewriteFormFileSchema(IOpenApiSchema? schema)
    {
        if (IsFormFileCollectionRef(schema))
            return FormFileArraySchema();

        if (IsFormFileRef(schema))
            return FormFileBinarySchema();

        if (schema is OpenApiSchema concreteSchema)
            InlineFormFileRefs(concreteSchema);

        return schema;
    }

    static void RewriteFormFileSchemaList(IList<IOpenApiSchema> schemas)
    {
        for (var i = 0; i < schemas.Count; i++)
            schemas[i] = RewriteFormFileSchema(schemas[i])!;
    }

    static bool IsFormFileRef(IOpenApiSchema? schema)
        => schema is OpenApiSchemaReference schemaRef &&
           GetReferenceId(schemaRef) is { } refId &&
           refId is "IFormFile";

    static bool IsFormFileCollectionRef(IOpenApiSchema? schema)
        => schema is OpenApiSchemaReference schemaRef &&
           GetReferenceId(schemaRef) is { } refId &&
           (refId is "IFormFileCollection" ||
            refId.Contains("IFormFileCollection", StringComparison.Ordinal) ||
            refId.Contains("IEnumerableOfIFormFile", StringComparison.Ordinal) ||
            refId.Contains("ListOfIFormFile", StringComparison.Ordinal) ||
            refId.Contains("IFormFile[]", StringComparison.Ordinal));

    static string? GetReferenceId(OpenApiSchemaReference schemaRef)
        => schemaRef.Reference.Id ?? schemaRef.Id;

    static OpenApiSchema FormFileBinarySchema()
        => new() { Type = JsonSchemaType.String, Format = "binary" };

    static OpenApiSchema FormFileArraySchema()
        => new()
        {
            Type = JsonSchemaType.Array,
            Items = FormFileBinarySchema()
        };

}
