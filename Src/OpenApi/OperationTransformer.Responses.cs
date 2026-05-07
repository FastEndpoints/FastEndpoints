using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
namespace FastEndpoints.OpenApi;

sealed class ResponseOperationTransformer(DocumentOptions docOpts, SharedContext sharedCtx)
{
        readonly ResponseHeaderFactory _headerFactory = new(docOpts, sharedCtx);

        static readonly Dictionary<string, string> _defaultDescriptions = new()
        {
            { "200", "Success" },
            { "201", "Created" },
            { "202", "Accepted" },
            { "204", "No Content" },
            { "400", "Bad Request" },
            { "401", "Unauthorized" },
            { "402", "Payment Required" },
            { "403", "Forbidden" },
            { "404", "Not Found" },
            { "405", "Method Not Allowed" },
            { "406", "Not Acceptable" },
            { "429", "Too Many Requests" },
            { "500", "Server Error" }
        };

        JsonNamingPolicy? NamingPolicy => sharedCtx.NamingPolicy;
        JsonSerializerOptions SerializerOptions => sharedCtx.SerializerOptions ?? Cfg.SerOpts.Options;

        public void AddMissingResponses(OpenApiOperation operation, IList<object> metadata)
        {
            operation.Responses ??= [];

            foreach (var meta in metadata.OfType<IProducesResponseTypeMetadata>())
            {
                var key = meta.StatusCode.ToString();
                var existing = operation.Responses.TryGetValue(key, out var existingResp) ? existingResp as OpenApiResponse : null;
                var isNew = existing is null;
                var response = existing ?? new OpenApiResponse();

                AddMissingResponseContent(response, meta);

                if (isNew)
                    operation.Responses[key] = response;
            }
        }

        public void ApplyDescriptions(OpenApiOperation operation, EndpointDefinition epDef, OpenApiOperationTransformerContext context, string operationKey)
        {
            if (operation.Responses is null)
                return;

            var responseTypes = epDef.EndpointSummary?.ResponseParams.Count > 0
                                    ? ResponseMetadataCatalog.BuildSupportedResponseTypeMap(context)
                                    : null;

            foreach (var (statusCode, response) in operation.Responses)
            {
                ApplyDefaultResponseDescription(statusCode, response);

                if (epDef.EndpointSummary is not null)
                {
                    var code = int.TryParse(statusCode, out var c) ? c : 0;
                    if (epDef.EndpointSummary.Responses.TryGetValue(code, out var customDesc))
                        response.Description = customDesc;

                    if (epDef.EndpointSummary.ResponseParams.TryGetValue(code, out var propDescriptions))
                        ApplyParamDescriptions(response, propDescriptions, responseTypes?.GetValueOrDefault(code), operationKey, $"response.{statusCode}");
                }
            }
        }

        public void FixBinaryFormats(OpenApiOperation operation, string operationKey)
        {
            if (operation.Responses is null)
                return;

            var mutationCtx = new OperationSchemaMutationContext(sharedCtx, operationKey);

            foreach (var response in operation.Responses.Values)
            {
                if (response is not OpenApiResponse concreteResp || concreteResp.Content is not { Count: > 0 })
                    continue;

                foreach (var (contentType, mediaType) in concreteResp.Content)
                {
                    // skip JSON content types; "byte" is correct for base64-encoded JSON string responses
                    if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (mediaType.Schema.ResolveSchema(sharedCtx) is not { Type: JsonSchemaType.String, Format: "byte" })
                        continue;

                    var schema = mediaType.Schema.EnsureSchemaForMutation(
                        mutationCtx,
                        $"response.{contentType}.binary",
                        localized => mediaType.Schema = localized);

                    if (schema is not null)
                        schema.Format = "binary";
                }
            }
        }

        public void ApplyExamples(OpenApiOperation operation, EndpointDefinition epDef, IList<object> metadata)
        {
            var examples = ResponseMetadataCatalog.BuildExamples(epDef, metadata);

            if (examples.Count == 0)
                return;

            foreach (var (statusCode, example) in examples)
            {
                var key = statusCode.ToString();

                if (operation.Responses?.TryGetValue(key, out var response) != true || response?.Content is null)
                    continue;

                var exampleNode = example.JsonNodeFromObject(SerializerOptions);

                foreach (var content in response.Content.Values)
                {
                    content.Example = exampleNode?.DeepClone();

                    // clear any framework-populated named examples to avoid invalid spec (OAS 3.1 forbids both)
                    content.Examples?.Clear();
                }
            }
        }

        public void AddHeaders(OpenApiOperation operation, EndpointDefinition epDef, IList<object> metadata)
        {
            if (operation.Responses is null)
                return;

            var responseTypeMetas = ResponseMetadataCatalog.BuildResponseTypeMetadataMap(metadata);
            var configuredHeaders = epDef.EndpointSummary?.ResponseHeaders is { Count: > 0 } headers
                                        ? ResponseMetadataCatalog.BuildHeadersByStatusCode(headers)
                                        : null;

            foreach (var (statusCode, response) in operation.Responses)
            {
                if (response is not OpenApiResponse concreteResponse)
                    continue;

                var code = int.TryParse(statusCode, out var c) ? c : 0;

                if (responseTypeMetas.TryGetValue(code, out var responseMeta) && responseMeta.Type is not null)
                    _headerFactory.AddTypedHeaders(concreteResponse, responseMeta.Type);

                if (configuredHeaders?.TryGetValue(code, out var headersForStatusCode) == true)
                    _headerFactory.AddConfiguredHeaders(concreteResponse, headersForStatusCode);
            }
        }

        public void FixPolymorphism(OpenApiOperation operation, string operationKey)
        {
            if (operation.Responses is null)
                return;

            var mutationCtx = new OperationSchemaMutationContext(sharedCtx, operationKey);

            foreach (var (_, response) in operation.Responses)
            {
                if (response.Content is null)
                    continue;

                foreach (var (_, mediaType) in response.Content)
                {
                    if (mediaType.Schema is null)
                        continue;

                    if (mediaType.Schema.ResolveSchemaOrReference(sharedCtx) is not OpenApiSchema actualSchema)
                        continue;

                    if (actualSchema.Discriminator?.Mapping is not { Count: > 0 } ||
                        actualSchema.OneOf is not { Count: > 0 })
                        continue;

                    // preserve existing schema metadata and only surface oneOf at the response schema level
                    if (mediaType.Schema.OneOf is { Count: > 0 })
                        continue;

                    if (mediaType.Schema.EnsureSchemaForMutation(
                            mutationCtx,
                            "response.polymorphism",
                            localized => mediaType.Schema = localized) is not OpenApiSchema responseSchema)
                        continue;

                    responseSchema.OneOf ??= [];

                    foreach (var schemaOption in actualSchema.OneOf)
                        responseSchema.OneOf.Add(schemaOption);
                }
            }
        }

        void ApplyParamDescriptions(IOpenApiResponse response,
                                    Dictionary<string, string> propDescriptions,
                                    Type? responseDtoType,
                                    string operationKey,
                                    string schemaKey)
        {
            if (response is not OpenApiResponse concreteResp || concreteResp.Content is not { Count: > 0 })
                return;

            var collectionElementType = responseDtoType is null ? null : OperationSchemaHelpers.TryGetCollectionElementType(responseDtoType);
            var descriptionType = collectionElementType ?? responseDtoType;
            var jsonNameToClrName = BuildJsonNameMap(descriptionType, NamingPolicy, docOpts.UsePropertyNamingPolicy);
            var mutationCtx = new OperationSchemaMutationContext(sharedCtx, operationKey);

            foreach (var content in concreteResp.Content.Values)
            {
                var schema = content.EnsureOperationLocalSchemaForMutation(mutationCtx, schemaKey);
                var descriptionTarget = collectionElementType is null ? schema : EnsureLocalArrayItemSchema(schema, mutationCtx, schemaKey);

                if (descriptionTarget?.Properties is not { Count: > 0 })
                    continue;

                foreach (var (propKey, propSchema) in descriptionTarget.Properties)
                {
                    var propName = jsonNameToClrName?.TryGetValue(propKey, out var clrName) == true ? clrName : propKey;

                    if (!propDescriptions.TryGetValue(propName, out var responseDescription))
                        continue;

                    var concretePropSchema = propSchema.EnsureSchemaForMutation(
                        mutationCtx,
                        $"{schemaKey}.{propKey}",
                        localized => descriptionTarget.Properties![propKey] = localized);

                    if (concretePropSchema is not null)
                        concretePropSchema.Description = responseDescription;
                }
            }
        }

        static OpenApiSchema? EnsureLocalArrayItemSchema(OpenApiSchema? schema, OperationSchemaMutationContext mutationCtx, string schemaKey)
        {
            if (schema?.Type?.HasFlag(JsonSchemaType.Array) != true)
                return null;

            return schema.Items.EnsureSchemaForMutation(
                mutationCtx,
                $"{schemaKey}.items",
                localized => schema.Items = localized,
                cloneConcreteSchema: true);
        }

        void AddMissingResponseContent(OpenApiResponse response, IProducesResponseTypeMetadata metadata)
        {
            if (metadata.Type is null || metadata.Type == Types.Void)
                return;

            response.Content ??= new Dictionary<string, OpenApiMediaType>();

            foreach (var contentType in metadata.ContentTypes)
            {
                if (!response.Content.ContainsKey(contentType))
                    response.Content[contentType] = CreateMissingResponseMediaType(metadata.Type, docOpts.ShortSchemaNames);
            }
        }

        static void ApplyDefaultResponseDescription(string statusCode, IOpenApiResponse response)
        {
            if (_defaultDescriptions.TryGetValue(statusCode, out var description) &&
                (string.IsNullOrWhiteSpace(response.Description) || IsFrameworkDefault(statusCode, response.Description)))
                response.Description = description;
        }

        static Dictionary<string, string>? BuildJsonNameMap(Type? type, JsonNamingPolicy? namingPolicy, bool usePropertyNamingPolicy)
        {
            if (type is null)
                return null;

            Dictionary<string, string>? jsonNameMap = null;

            foreach (var property in OperationReflectionCache.GetTypeMetadata(type).PublicInstanceProperties)
            {
                var jsonName = PropertyNameResolver.GetSchemaPropertyName(property, namingPolicy, usePropertyNamingPolicy);
                jsonNameMap ??= [];
                jsonNameMap[jsonName] = property.Name;
            }

            return jsonNameMap;
        }

        OpenApiMediaType CreateMissingResponseMediaType(Type type, bool shortSchemaNames)
            => new()
            {
                Schema = type.GetSchemaForType(sharedCtx, shortSchemaNames)
            };

        static bool IsFrameworkDefault(string statusCode, string description)
            => statusCode switch
            {
                "200" => description == "OK",
                "201" => description == "Created",
                "204" => description == "No Content",
                "500" => description == "Internal Server Error",
                _ => false
            };
}
