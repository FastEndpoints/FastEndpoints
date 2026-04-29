using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed partial class OperationTransformer
{
    sealed class ResponseOperationTransformer(DocumentOptions docOpts, SharedContext sharedCtx)
    {
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

        public void ApplyDescriptions(OpenApiOperation operation, EndpointDefinition epDef, OpenApiOperationTransformerContext context)
        {
            if (operation.Responses is null)
                return;

            var responseTypes = BuildSupportedResponseTypeMap(context);

            foreach (var (statusCode, response) in operation.Responses)
            {
                ApplyDefaultResponseDescription(statusCode, response);

                if (epDef.EndpointSummary is not null)
                {
                    var code = int.TryParse(statusCode, out var c) ? c : 0;
                    if (epDef.EndpointSummary.Responses.TryGetValue(code, out var customDesc))
                        response.Description = customDesc;

                    if (epDef.EndpointSummary.ResponseParams.TryGetValue(code, out var propDescriptions))
                        ApplyParamDescriptions(response, propDescriptions, responseTypes.GetValueOrDefault(code));
                }
            }
        }

        public void FixBinaryFormats(OpenApiOperation operation)
        {
            if (operation.Responses is null)
                return;

            foreach (var response in operation.Responses.Values)
            {
                if (response is not OpenApiResponse concreteResp || concreteResp.Content is not { Count: > 0 })
                    continue;

                foreach (var (contentType, mediaType) in concreteResp.Content)
                {
                    // skip JSON content types; "byte" is correct for base64-encoded JSON string responses
                    if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (mediaType.Schema is OpenApiSchema { Type: JsonSchemaType.String, Format: "byte" } schema)
                        schema.Format = "binary";
                }
            }
        }

        public void ApplyExamples(OpenApiOperation operation, EndpointDefinition epDef)
        {
            if (epDef.EndpointSummary?.ResponseExamples.Count is not > 0)
                return;

            foreach (var (statusCode, example) in epDef.EndpointSummary.ResponseExamples)
            {
                var key = statusCode.ToString();

                if (operation.Responses?.TryGetValue(key, out var response) != true || response?.Content is null)
                    continue;

                var exampleNode = example.JsonNodeFromObject();

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

            var responseTypeMetas = metadata.OfType<IProducesResponseTypeMetadata>()
                                            .GroupBy(m => m.StatusCode)
                                            .ToDictionary(g => g.Key, g => g.Last());

            foreach (var (statusCode, response) in operation.Responses)
            {
                if (response is not OpenApiResponse concreteResponse)
                    continue;

                var code = int.TryParse(statusCode, out var c) ? c : 0;

                if (responseTypeMetas.TryGetValue(code, out var responseMeta) && responseMeta.Type is not null)
                    AddTypedResponseHeaders(concreteResponse, responseMeta.Type);

                if (epDef.EndpointSummary?.ResponseHeaders is { Count: > 0 })
                    AddConfiguredResponseHeaders(concreteResponse, epDef.EndpointSummary.ResponseHeaders, code);
            }
        }

        public void FixPolymorphism(OpenApiOperation operation)
        {
            if (operation.Responses is null)
                return;

            foreach (var (_, response) in operation.Responses)
            {
                if (response.Content is null)
                    continue;

                foreach (var (_, mediaType) in response.Content)
                {
                    if (mediaType.Schema is null)
                        continue;

                    if (mediaType.Schema.ResolveSchemaOrReference() is not OpenApiSchema actualSchema)
                        continue;

                    if (actualSchema.Discriminator?.Mapping is not { Count: > 0 } ||
                        actualSchema.OneOf is not { Count: > 0 })
                        continue;

                    // preserve existing schema metadata and only surface oneOf at the response schema level
                    if (mediaType.Schema.OneOf is { Count: > 0 })
                        continue;

                    if (mediaType.Schema is not OpenApiSchema responseSchema)
                        continue;

                    responseSchema.OneOf ??= [];

                    foreach (var schemaOption in actualSchema.OneOf)
                        responseSchema.OneOf.Add(schemaOption);
                }
            }
        }

        void ApplyParamDescriptions(IOpenApiResponse response,
                                    Dictionary<string, string> propDescriptions,
                                    Type? responseDtoType)
        {
            if (response is not OpenApiResponse concreteResp || concreteResp.Content is not { Count: > 0 })
                return;

            var jsonNameToClrName = BuildJsonNameMap(responseDtoType, NamingPolicy, docOpts.UsePropertyNamingPolicy);

            foreach (var content in concreteResp.Content.Values)
            {
                var schema = content.EnsureOperationLocalSchemaForMutation();

                if (schema?.Properties is not { Count: > 0 })
                    continue;

                foreach (var (propKey, propSchema) in schema.Properties)
                {
                    var propName = jsonNameToClrName?.TryGetValue(propKey, out var clrName) == true ? clrName : propKey;

                    if (propDescriptions.TryGetValue(propName, out var responseDescription) && propSchema is OpenApiSchema concretePropSchema)
                        concretePropSchema.Description = responseDescription;
                }
            }
        }

        static Dictionary<int, Type?> BuildSupportedResponseTypeMap(OpenApiOperationTransformerContext context)
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

        void AddTypedResponseHeaders(OpenApiResponse response, Type responseType)
        {
            foreach (var prop in GetPublicInstanceProperties(responseType))
            {
                var toHeaderAttr = prop.GetCustomAttribute<ToHeaderAttribute>();

                if (toHeaderAttr is null)
                    continue;

                var headerName = toHeaderAttr.HeaderName ?? prop.Name.ApplyPropNamingPolicy(docOpts, NamingPolicy);
                var headerType = prop.PropertyType.Name.EndsWith("HeaderValue", StringComparison.Ordinal) ? typeof(string) : prop.PropertyType;

                AddResponseHeader(
                    response,
                    headerName,
                    new()
                    {
                        Schema = headerType.GetSchemaForType(sharedCtx, docOpts.ShortSchemaNames),
                        Example = headerType.GetSampleValue().JsonNodeFromObject()
                    });
            }
        }

        void AddConfiguredResponseHeaders(OpenApiResponse response, IEnumerable<ResponseHeader> headers, int statusCode)
        {
            foreach (var header in headers.Where(h => h.StatusCode == statusCode))
            {
                var example = header.Example.JsonNodeFromObject();

                AddResponseHeader(
                    response,
                    header.HeaderName,
                    new()
                    {
                        Description = header.Description,
                        Example = example,
                        Schema = CreateConfiguredResponseHeaderSchema(header.Example, example)
                    });
            }
        }

        void AddMissingResponseContent(OpenApiResponse response, IProducesResponseTypeMetadata metadata)
        {
            if (metadata.Type is null || metadata.Type == Types.Void || !metadata.ContentTypes.Any())
                return;

            response.Content ??= new Dictionary<string, OpenApiMediaType>();

            foreach (var contentType in metadata.ContentTypes)
            {
                if (!response.Content.ContainsKey(contentType))
                    response.Content[contentType] = CreateMissingResponseMediaType(metadata.Type, docOpts.ShortSchemaNames);
            }
        }

        IOpenApiSchema? CreateConfiguredResponseHeaderSchema(object? exampleValue, JsonNode? exampleNode)
        {
            if (exampleValue is null)
                return null;

            var exampleType = exampleValue.GetType();

            if (!IsAnonymousType(exampleType))
                return exampleType.GetSchemaForType(sharedCtx, docOpts.ShortSchemaNames);

            return OperationSchemaHelpers.CreateSchemaFromExampleNode(exampleNode);
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

            foreach (var property in GetTypeMetadata(type).PublicInstanceProperties)
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

        static bool IsAnonymousType(Type type)
        {
            if (!Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), inherit: false))
                return false;

            if (!type.IsGenericType)
                return false;

            var name = type.Name;

            return name.Contains("AnonymousType", StringComparison.Ordinal) &&
                   (name.StartsWith("<>", StringComparison.Ordinal) || name.StartsWith("VB$", StringComparison.Ordinal)) &&
                   !type.IsPublic;
        }

        static void AddResponseHeader(OpenApiResponse response, string headerName, OpenApiHeader header)
        {
            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers[headerName] = header;
        }

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
}
