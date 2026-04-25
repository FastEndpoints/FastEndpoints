using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed partial class OperationTransformer
{
    sealed class RequestTransformState
    {
        public HashSet<string> PropsRemovedFromBody { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    sealed class RequestOperationTransformer(DocumentOptions docOpts, SharedContext sharedCtx)
    {
        static readonly string[] _illegalHeaderNames = ["Accept", "Content-Type", "Authorization"];
        JsonNamingPolicy? NamingPolicy => sharedCtx.NamingPolicy;

        public RequestTransformState HandleParameters(OpenApiOperation operation,
                                                      OpenApiOperationTransformerContext context,
                                                      EndpointDefinition epDef,
                                                      string documentPath)
        {
            var state = new RequestTransformState();
            var endpointRouteTemplate = FindEndpointRouteTemplate(epDef, documentPath);
            var routeParameters = GetRouteParameters(endpointRouteTemplate ?? context.Description.RelativePath ?? documentPath);

            var requestDtoType = GetRequestDtoType(epDef);

            if (requestDtoType is not null && requestDtoType != Types.EmptyRequest)
            {
                var isGetRequest = context.Description.HttpMethod == "GET";
                var requestDtoIsList = requestDtoType.IsCollection();
                var requestDtoProps = requestDtoIsList
                                          ? null
                                          : GetPublicInstanceProperties(requestDtoType).ToList();

                if (requestDtoProps is not null)
                {
                    RemoveHiddenProperties(operation, requestDtoProps, state);
                    AddBoundParameters(operation, requestDtoProps, routeParameters, isGetRequest, state);
                }

                // remove request body if GET request (unless explicitly enabled) or if empty
                if ((isGetRequest && !docOpts.EnableGetRequestsWithBody) || operation.IsRequestBodyEmpty())
                {
                    if (!requestDtoIsList)
                        operation.RequestBody = null;
                }
            }

            EnsureRouteParameters(operation, routeParameters);

            return state;
        }

        public void ApplyBodyOverrides(OpenApiOperation operation, EndpointDefinition epDef)
        {
            if (operation.RequestBody?.Content is null)
                return;

            var requestDtoType = GetRequestDtoType(epDef);

            if (requestDtoType is null)
                return;

            var requestDtoProps = GetPublicInstanceProperties(requestDtoType);

            var fromBodyProp = requestDtoProps
                .FirstOrDefault(p => p.IsDefined(Types.FromBodyAttribute, false));

            var fromFormProp = requestDtoProps
                .FirstOrDefault(p => p.IsDefined(Types.FromFormAttribute, false));

            var promoteProp = fromBodyProp ?? fromFormProp;
            if (promoteProp is null)
                return;

            var promoted = false;

            // replace the entire request body schema with the [FromBody]/[FromForm] property's type schema
            foreach (var content in operation.RequestBody.Content.Values)
            {
                var resolvedSchema = content.Schema.ResolveSchema();

                if (resolvedSchema is null)
                    continue;

                var schemaKey = PropertyNameResolver.GetSchemaPropertyName(promoteProp, NamingPolicy);
                var matchingKey = resolvedSchema.Properties?.Keys
                                                .FirstOrDefault(k => string.Equals(k, schemaKey, StringComparison.OrdinalIgnoreCase));

                if (matchingKey is not null && resolvedSchema.Properties!.TryGetValue(matchingKey, out var propSchema))
                {
                    content.Schema = propSchema;
                    promoted = true;
                }
            }

            if (promoted && SchemaNameGenerator.GetReferenceId(requestDtoType, docOpts.ShortSchemaNames) is { } refId)
                sharedCtx.PromotedRequestWrapperSchemaRefs.TryAdd(refId, 0);

            // JSON Patch unwrap: only for [FromBody], promote the operations array to top-level
            if (fromBodyProp is not null && operation.RequestBody.Content.TryGetValue("application/json-patch+json", out var patchContent))
            {
                var patchArraySchema = TryGetJsonPatchArraySchema(patchContent.Schema);

                if (patchArraySchema is not null)
                    patchContent.Schema = patchArraySchema;
            }
        }

        public void ApplyParameterMetadata(OpenApiOperation operation, EndpointDefinition epDef)
        {
            if (operation.Parameters is not { Count: > 0 })
                return;

            var requestDtoType = GetRequestDtoType(epDef);
            var requestProps = requestDtoType is null ? null : GetPublicInstanceProperties(requestDtoType);

            foreach (var param in operation.Parameters)
            {
                if (param is not OpenApiParameter concreteParam)
                    continue;

                var prop = concreteParam.Name is { } parameterName
                               ? requestProps?.FirstOrDefault(p => MatchesParameterName(p, parameterName, concreteParam.In ?? ParameterLocation.Query))
                               : null;

                if (epDef.EndpointSummary?.Params is { Count: > 0 })
                {
                    var descriptionKey = prop?.Name ?? concreteParam.Name;
                    var description = descriptionKey is not null
                                          ? FindParamDescription(epDef.EndpointSummary.Params, descriptionKey)
                                          : null;
                    if (description is not null)
                        concreteParam.Description = description;
                }

                if (string.IsNullOrWhiteSpace(concreteParam.Description) && prop is not null)
                    concreteParam.Description = XmlDocLookup.GetPropertySummary(prop);

                if (prop is not null)
                {
                    var defaultAttr = prop.GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>();

                    if (defaultAttr?.Value is not null && concreteParam.Schema is OpenApiSchema paramSchema)
                        paramSchema.Default = defaultAttr.Value.JsonNodeFromObject();
                }
            }

            ApplyExampleRequestToParams(operation, epDef);
        }

        public void ApplyExamples(OpenApiOperation operation, EndpointDefinition epDef, RequestTransformState state)
        {
            if (epDef.EndpointSummary?.RequestExamples.Count is not > 0)
                return;

            if (operation.RequestBody?.Content is null)
                return;

            var examples = BuildUniqueRequestExamples(epDef.EndpointSummary.RequestExamples);
            var fallbackExample = BuildRequestExampleFallback(epDef, state.PropsRemovedFromBody);

            foreach (var content in operation.RequestBody.Content.Values)
            {
                var schema = content.Schema.ResolveSchema();

                if (examples.Count == 1)
                {
                    content.Example = NormalizeExampleNode(StripRemovedProps(examples[0].Value.JsonNodeFromObject(), state.PropsRemovedFromBody), schema, fallbackExample);
                    content.Examples?.Clear();
                }
                else
                {
                    content.Example = null;
                    content.Examples ??= new Dictionary<string, IOpenApiExample>();

                    foreach (var example in examples)
                    {
                        content.Examples[example.Label] = new OpenApiExample
                        {
                            Summary = example.Summary,
                            Description = example.Description,
                            Value = NormalizeExampleNode(StripRemovedProps(example.Value.JsonNodeFromObject(), state.PropsRemovedFromBody), schema, fallbackExample)
                        };
                    }
                }
            }
        }

        public void ApplyParamDescriptionsToBodySchema(OpenApiOperation operation, EndpointDefinition epDef, RequestTransformState state)
        {
            if (operation.RequestBody?.Content is null)
                return;

            var hasParams = epDef.EndpointSummary?.Params is { Count: > 0 };
            var exampleObj = epDef.EndpointSummary?.ExampleRequest;
            var fallbackExample = BuildRequestExampleFallback(epDef, state.PropsRemovedFromBody);

            if (!hasParams && exampleObj is null)
                return;

            Dictionary<string, JsonNode>? propExamples = null;

            if (exampleObj is not null and not IEnumerable &&
                exampleObj.JsonObjectFromObject() is { } obj)
            {
                propExamples = [];

                foreach (var (key, value) in obj)
                {
                    if (value is not null)
                        propExamples[key] = value.DeepClone();
                }
            }

            foreach (var content in operation.RequestBody.Content.Values)
            {
                var schema = content.EnsureOperationLocalSchemaIfShared(sharedCtx);

                if (schema is null)
                    continue;

                if (exampleObj is not null)
                {
                    var exNode = exampleObj.JsonNodeFromObject();

                    if (exNode is not null)
                    {
                        if (state.PropsRemovedFromBody.Count > 0 && exNode is JsonObject exObj)
                            exObj.RemoveProperties(state.PropsRemovedFromBody);

                        schema.Example = NormalizeExampleNode(exNode, schema, fallbackExample);
                    }
                }

                if (schema.Properties is null)
                    continue;

                foreach (var (propName, propSchema) in schema.Properties)
                {
                    if (propSchema is not OpenApiSchema concreteProp)
                        continue;

                    if (hasParams && epDef.EndpointSummary?.Params != null)
                    {
                        var description = FindParamDescription(epDef.EndpointSummary.Params, propName);
                        if (description is not null)
                            concreteProp.Description = description;
                    }

                    if (propExamples is not null && propExamples.TryGetValue(propName, out var exVal))
                        concreteProp.Example = exVal;
                }
            }
        }

        bool MatchesParameterName(PropertyInfo property, string parameterName, ParameterLocation location)
            => string.Equals(GetEffectiveParameterName(property, location), parameterName, StringComparison.OrdinalIgnoreCase);

        string GetEffectiveParameterName(PropertyInfo property, ParameterLocation location)
        {
            if (location == ParameterLocation.Header)
                return property.GetCustomAttribute<FromHeaderAttribute>()?.HeaderName ?? property.Name.ApplyPropNamingPolicy(docOpts, NamingPolicy);

            if (location == ParameterLocation.Cookie)
                return property.GetCustomAttribute<FromCookieAttribute>()?.CookieName ?? property.Name.ApplyPropNamingPolicy(docOpts, NamingPolicy);

            if (location == ParameterLocation.Path)
                return property.GetCustomAttribute<BindFromAttribute>()?.Name?.ApplyPropNamingPolicy(docOpts, NamingPolicy) ??
                       property.Name.ApplyPropNamingPolicy(docOpts, NamingPolicy);

            if (location == ParameterLocation.Query)
                return property.GetCustomAttribute<BindFromAttribute>()?.Name ?? PropertyNameResolver.GetSchemaPropertyName(property, NamingPolicy);

            return property.Name;
        }

        static void ApplyExampleRequestToParams(OpenApiOperation operation, EndpointDefinition epDef)
        {
            var exampleRequest = epDef.EndpointSummary?.ExampleRequest;

            if (exampleRequest is null || operation.Parameters is not { Count: > 0 })
                return;

            var exampleObj = exampleRequest.JsonObjectFromObject(exampleRequest.GetType());

            if (exampleObj is null)
                return;

            foreach (var param in operation.Parameters)
            {
                if (param is not OpenApiParameter concreteParam)
                    continue;

                // look up by param name in the serialized JSON (case-insensitive)
                var matchingProp = exampleObj.FirstOrDefault(kv => string.Equals(kv.Key, concreteParam.Name, StringComparison.OrdinalIgnoreCase));

                if (matchingProp.Value is not null)
                    concreteParam.Example = matchingProp.Value.DeepClone();
            }
        }

        static string? FindParamDescription(IReadOnlyDictionary<string, string> paramDescriptions, string key)
        {
            var matchingKey = paramDescriptions.Keys.FindCaseInsensitiveKey(key);

            return matchingKey is not null ? paramDescriptions[matchingKey] : null;
        }

        void RemoveHiddenProperties(OpenApiOperation operation, List<PropertyInfo> requestDtoProps, RequestTransformState state)
        {
            for (var i = requestDtoProps.Count - 1; i >= 0; i--)
            {
                var p = requestDtoProps[i];

                if (p.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always &&
                    !p.IsDefined(Types.HideFromDocsAttribute) &&
                    p.GetSetMethod()?.IsPublic is true)
                    continue;

                requestDtoProps.RemoveAt(i);
                operation.RemovePropFromRequestBody(p, sharedCtx, NamingPolicy, state.PropsRemovedFromBody);
            }
        }

        void AddBoundParameters(OpenApiOperation operation,
                                List<PropertyInfo> requestDtoProps,
                                List<RouteParameterInfo> routeParameters,
                                bool isGetRequest,
                                RequestTransformState state)
        {
            for (var i = 0; i < requestDtoProps.Count; i++)
            {
                var p = requestDtoProps[i];

                AddAttributeParameters(operation, p, state);
                AddRouteParameter(operation, p, routeParameters, state);

                var queryParamName = p.Name.ApplyPropNamingPolicy(docOpts, NamingPolicy);

                if (ShouldAddQueryParam(p, operation, queryParamName, isGetRequest && !docOpts.EnableGetRequestsWithBody))
                {
                    operation.RemovePropFromRequestBody(p, sharedCtx, NamingPolicy, state.PropsRemovedFromBody);
                    AddParameter(operation, queryParamName, ParameterLocation.Query, p, shortSchemaNames: docOpts.ShortSchemaNames);
                }
            }
        }

        void AddAttributeParameters(OpenApiOperation operation, PropertyInfo p, RequestTransformState state)
        {
            foreach (var attribute in p.GetCustomAttributes())
            {
                switch (attribute)
                {
                    case FromHeaderAttribute hAttrib:
                    {
                        var pName = hAttrib.HeaderName ?? p.Name.ApplyPropNamingPolicy(docOpts, NamingPolicy);

                        if (IsIllegalHeaderName(pName))
                        {
                            operation.RemovePropFromRequestBody(p, sharedCtx, NamingPolicy, state.PropsRemovedFromBody);

                            continue;
                        }

                        AddParameter(operation, pName, ParameterLocation.Header, p, hAttrib.IsRequired, docOpts.ShortSchemaNames);

                        if (hAttrib.IsRequired || hAttrib.RemoveFromSchema)
                            operation.RemovePropFromRequestBody(p, sharedCtx, NamingPolicy, state.PropsRemovedFromBody);

                        break;
                    }

                    case FromCookieAttribute cAttrib:
                    {
                        var pName = cAttrib.CookieName ?? p.Name.ApplyPropNamingPolicy(docOpts, NamingPolicy);
                        AddParameter(operation, pName, ParameterLocation.Cookie, p, cAttrib.IsRequired, docOpts.ShortSchemaNames);

                        if (cAttrib.IsRequired || cAttrib.RemoveFromSchema)
                            operation.RemovePropFromRequestBody(p, sharedCtx, NamingPolicy, state.PropsRemovedFromBody);

                        break;
                    }

                    case FromClaimAttribute cAttrib when cAttrib.IsRequired || cAttrib.RemoveFromSchema:
                    case HasPermissionAttribute pAttrib when pAttrib.IsRequired || pAttrib.RemoveFromSchema:
                        operation.RemovePropFromRequestBody(p, sharedCtx, NamingPolicy, state.PropsRemovedFromBody);

                        break;
                }
            }
        }

        void AddRouteParameter(OpenApiOperation operation, PropertyInfo p, List<RouteParameterInfo> routeParameters, RequestTransformState state)
        {
            var bindName = p.GetCustomAttribute<BindFromAttribute>()?.Name ?? p.Name;
            var matchingRouteParam = routeParameters.FirstOrDefault(rp => string.Equals(rp.Name, bindName, StringComparison.OrdinalIgnoreCase));

            if (matchingRouteParam is null)
                return;

            operation.RemovePropFromRequestBody(p, sharedCtx, NamingPolicy, state.PropsRemovedFromBody);

            var appliedName = matchingRouteParam.Name.GetOpenApiRouteParameterName(docOpts, NamingPolicy);

            if (TryNormalizeExistingPathParameter(operation, matchingRouteParam.Name, appliedName, p.PropertyType))
                return;

            if (!HasParameter(operation, ParameterLocation.Path, appliedName))
                AddParameter(operation, appliedName, ParameterLocation.Path, p, true, docOpts.ShortSchemaNames);
            else
                UpdateParameterSchema(operation, ParameterLocation.Path, appliedName, p.PropertyType, docOpts.ShortSchemaNames);
        }

        void EnsureRouteParameters(OpenApiOperation operation, List<RouteParameterInfo> routeParameters)
        {
            for (var i = 0; i < routeParameters.Count; i++)
            {
                var routeParam = routeParameters[i];
                var appliedName = routeParam.Name.GetOpenApiRouteParameterName(docOpts, NamingPolicy);
                var resolvedType = routeParam.ConstraintType;

                if (TryNormalizeExistingPathParameter(operation, routeParam.Name, appliedName, resolvedType))
                    continue;

                AddParameter(operation, appliedName, ParameterLocation.Path, null, true, docOpts.ShortSchemaNames, resolvedType);
            }
        }

        bool TryNormalizeExistingPathParameter(OpenApiOperation operation, string routeParamName, string appliedName, Type? schemaType)
        {
            var existing = FindPathParameter(operation, appliedName) ?? FindPathParameter(operation, routeParamName);

            if (existing is null)
                return false;

            if (!string.Equals(existing.Name, appliedName, StringComparison.Ordinal))
                existing.Name = appliedName;

            if (schemaType is not null)
                UpdateParameterSchema(operation, ParameterLocation.Path, appliedName, schemaType, docOpts.ShortSchemaNames);

            return true;

            static OpenApiParameter? FindPathParameter(OpenApiOperation operation, string name)
            {
                if (operation.Parameters is not { Count: > 0 })
                    return null;

                foreach (var param in operation.Parameters)
                    if (param is OpenApiParameter { In: ParameterLocation.Path, Name: not null } concreteParam &&
                        string.Equals(concreteParam.Name, name, StringComparison.OrdinalIgnoreCase))
                        return concreteParam;

                return null;
            }
        }

        void AddParameter(OpenApiOperation operation,
                          string name,
                          ParameterLocation location,
                          PropertyInfo? prop,
                          bool? isRequired = null,
                          bool shortSchemaNames = false,
                          Type? explicitType = null)
        {
            operation.Parameters ??= [];

            var propType = explicitType ?? prop?.PropertyType ?? typeof(string);

            // typed header values (e.g. ContentDispositionHeaderValue) are transmitted as strings
            if (propType.Name.EndsWith("HeaderValue"))
                propType = typeof(string);

            var schema = propType.GetSchemaForType(sharedCtx, shortSchemaNames);
            var isNullable = prop is not null && IsNullable(prop);
            var required = isRequired ?? !isNullable;

            var param = new OpenApiParameter
            {
                Name = name,
                In = location,
                Required = required,
                Schema = schema
            };

            if (ShouldUseJsonParameterContent(location, propType))
            {
                param.Schema = null;
                param.Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new() { Schema = schema }
                };
            }

            if (required && prop is not null)
            {
                JsonNode? example = null;
                var xmlExample = XmlDocLookup.GetPropertyExample(prop);

                if (xmlExample is not null)
                {
                    try
                    {
                        example = JsonNode.Parse(xmlExample);
                    }
                    catch
                    {
                        // not valid JSON, ignore
                    }
                }

                example ??= propType.GenerateSampleJsonNode(NamingPolicy);

                if (example is not null)
                {
                    if (param.Content is not null)
                        param.Content["application/json"].Example = example;
                    else
                        param.Example = example;
                }
            }

            operation.Parameters.Add(param);
        }

        JsonNode? BuildRequestExampleFallback(EndpointDefinition epDef, HashSet<string> propsRemovedFromBody)
        {
            var fallback = GetRequestDtoType(epDef)?.GenerateSampleJsonNode(NamingPolicy);

            if (fallback is JsonObject fallbackObj && propsRemovedFromBody.Count > 0)
                fallbackObj.RemoveProperties(propsRemovedFromBody);

            return fallback;
        }

        static bool IsIllegalHeaderName(string name)
        {
            for (var i = 0; i < _illegalHeaderNames.Length; i++)
            {
                if (_illegalHeaderNames[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static bool ShouldAddQueryParam(PropertyInfo prop, OpenApiOperation operation, string queryParamName, bool isGetRequest)
        {
            foreach (var attribute in prop.GetCustomAttributes())
            {
                switch (attribute)
                {
                    case BindFromAttribute:
                    case FromHeaderAttribute:
                        return false;
                    case FromClaimAttribute cAttrib:
                        return !cAttrib.IsRequired;
                    case HasPermissionAttribute pAttrib:
                        return !pAttrib.IsRequired;
                }
            }

            if (operation.Parameters?.Any(p => string.Equals(p.Name, queryParamName, StringComparison.OrdinalIgnoreCase)) == true)
                return false;

            return isGetRequest || prop.IsDefined(Types.QueryParamAttribute);
        }

        static bool ShouldUseJsonParameterContent(ParameterLocation location, Type type)
        {
            if (location != ParameterLocation.Query)
                return false;

            type = Nullable.GetUnderlyingType(type) ?? type;

            return type.IsComplexType() && !type.IsCollection() ||
                   OperationSchemaHelpers.TryGetDictionaryValueType(type) is not null;
        }

        static OpenApiSchema? TryGetJsonPatchArraySchema(IOpenApiSchema? schema)
        {
            var resolved = schema.ResolveSchema();

            if (resolved is not { Type: JsonSchemaType.Object, Properties.Count: 1 })
                return null;

            var operationsProp = resolved.Properties
                                         .FirstOrDefault(p => string.Equals(p.Key, "operations", StringComparison.OrdinalIgnoreCase))
                                         .Value;

            return operationsProp.ResolveSchema() is { Type: JsonSchemaType.Array } arraySchema
                       ? arraySchema
                       : null;
        }

        static JsonNode? StripRemovedProps(JsonNode? node, HashSet<string> removedProps)
        {
            if (removedProps.Count == 0 || node is not JsonObject obj)
                return node;

            obj.RemoveProperties(removedProps);

            return obj;
        }

        static List<RequestExample> BuildUniqueRequestExamples(ICollection<RequestExample> examples)
        {
            var result = examples.ToList();

            foreach (var group in result.GroupBy(e => e.Label).Where(g => g.Count() > 1).ToList())
            {
                var i = 1;

                foreach (var ex in group)
                {
                    result[result.IndexOf(ex)] = new(ex.Value, $"{ex.Label} {i}", ex.Summary, ex.Description);
                    i++;
                }
            }

            return result;
        }

        static JsonNode? NormalizeExampleNode(JsonNode? example, OpenApiSchema? schema, JsonNode? fallback)
        {
            if (example is null)
                return AllowsNull(schema) ? null : fallback?.DeepClone() ?? CreateSampleFromSchema(schema);

            return example switch
            {
                JsonObject obj => NormalizeObjectExample(obj, schema, fallback as JsonObject),
                JsonArray arr => NormalizeArrayExample(arr, schema, fallback as JsonArray),
                _ => example
            };
        }

        static JsonObject NormalizeObjectExample(JsonObject example, OpenApiSchema? schema, JsonObject? fallback)
        {
            if (schema?.Properties is not { Count: > 0 } properties)
                return example;

            foreach (var key in example.Select(kvp => kvp.Key).ToArray())
            {
                var schemaKey = properties.Keys.FindCaseInsensitiveKey(key);

                if (schemaKey is null || properties[schemaKey].ResolveSchema() is not { } propertySchema)
                    continue;

                var fallbackKey = fallback?.Select(kvp => kvp.Key).FindCaseInsensitiveKey(key);
                var fallbackNode = fallbackKey is not null ? fallback![fallbackKey] : null;

                example[key] = NormalizeExampleNode(example[key], propertySchema, fallbackNode);
            }

            return example;
        }

        static JsonArray NormalizeArrayExample(JsonArray example, OpenApiSchema? schema, JsonArray? fallback)
        {
            var itemSchema = schema?.Items.ResolveSchema();

            if (itemSchema is null)
                return example;

            var fallbackNode = fallback is { Count: > 0 } ? fallback[0] : null;

            for (var i = 0; i < example.Count; i++)
                example[i] = NormalizeExampleNode(example[i], itemSchema, fallbackNode);

            return example;
        }

        static bool AllowsNull(OpenApiSchema? schema)
        {
            if (schema is null)
                return true;

            if (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Null))
                return true;

            if (schema.OneOf?.Any(s => AllowsNull(s.ResolveSchema())) == true)
                return true;

            return schema.AnyOf?.Any(s => AllowsNull(s.ResolveSchema())) == true;
        }

        static JsonNode? CreateSampleFromSchema(OpenApiSchema? schema, string? propertyName = null)
        {
            if (schema is null)
                return null;

            if (schema.OneOf is { Count: > 0 })
            {
                foreach (var option in schema.OneOf)
                {
                    var resolved = option.ResolveSchema();

                    if (resolved is not null && !AllowsNull(resolved))
                        return CreateSampleFromSchema(resolved, propertyName);
                }
            }

            if (schema.AnyOf is { Count: > 0 })
            {
                foreach (var option in schema.AnyOf)
                {
                    var resolved = option.ResolveSchema();

                    if (resolved is not null && !AllowsNull(resolved))
                        return CreateSampleFromSchema(resolved, propertyName);
                }
            }

            if (schema.Properties is { Count: > 0 })
            {
                var obj = new JsonObject();

                foreach (var (key, propertySchema) in schema.Properties)
                {
                    var sample = CreateSampleFromSchema(propertySchema.ResolveSchema(), key);

                    if (sample is not null)
                        obj[key] = sample;
                }

                return obj.Count > 0 ? obj : null;
            }

            if (schema.Type == JsonSchemaType.Array)
            {
                var itemSample = CreateSampleFromSchema(schema.Items.ResolveSchema(), propertyName);

                return itemSample is not null ? new JsonArray(itemSample) : new JsonArray();
            }

            return schema.Type switch
            {
                JsonSchemaType.String => (propertyName ?? string.Empty).JsonNodeFromObject(),
                JsonSchemaType.Integer => 0.JsonNodeFromObject(),
                JsonSchemaType.Number => 0m.JsonNodeFromObject(),
                JsonSchemaType.Boolean => false.JsonNodeFromObject(),
                _ => null
            };
        }
    }
}
