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
        public JsonNode? RequestBodyFallbackExample { get; set; }
        public bool RequestBodyFallbackExampleCreated { get; set; }
    }

    sealed record PromotedBodyProperty(string Name, Type Type);

    sealed partial class RequestOperationTransformer(DocumentOptions docOpts, SharedContext sharedCtx)
    {
        static readonly HashSet<string> _illegalHeaderNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Accept",
            "Content-Type",
            "Authorization"
        };
        static readonly ParameterLookupKeyComparer _parameterLookupKeyComparer = new();

        JsonNamingPolicy? NamingPolicy => sharedCtx.NamingPolicy;

        public RequestTransformState HandleParameters(OpenApiOperation operation, OpenApiOperationTransformerContext context, EndpointDefinition epDef, string documentPath)
        {
            var state = new RequestTransformState();
            var endpointRouteTemplate = FindEndpointRouteTemplate(epDef, documentPath);
            var routeParameters = GetRouteParameters(endpointRouteTemplate ?? context.Description.RelativePath ?? documentPath);
            var routeParameterLookup = BuildRouteParameterLookup(routeParameters);

            var requestDtoType = GetRequestDtoType(epDef);

            if (requestDtoType is not null && requestDtoType != Types.EmptyRequest)
            {
                var isGetRequest = context.Description.HttpMethod == "GET";
                var requestDtoIsList = requestDtoType.IsCollection();
                var requestDtoProps = requestDtoIsList
                                          ? null
                                          : GetPublicInstanceProperties(requestDtoType).ToList();

                ValidateRequestDto(epDef, requestDtoType, requestDtoIsList);

                if (requestDtoProps is not null)
                {
                    RemoveHiddenProperties(operation, requestDtoProps, state);
                    AddBoundParameters(operation, requestDtoProps, routeParameterLookup, isGetRequest, state);
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

        public PromotedBodyProperty? ApplyBodyOverrides(OpenApiOperation operation, EndpointDefinition epDef)
        {
            if (operation.RequestBody?.Content is null)
                return null;

            var requestDtoType = GetRequestDtoType(epDef);

            if (requestDtoType is null)
                return null;

            var (promoteProp, fromBodyProp, fromFormProp) = FindPromotedBodyProperty(requestDtoType);

            if (promoteProp is null)
                return null;

            var promoted = false;
            var schemaKey = PropertyNameResolver.GetSchemaPropertyName(promoteProp, NamingPolicy, docOpts.UsePropertyNamingPolicy);

            // replace the entire request body schema with the [FromBody]/[FromForm] property's type schema
            foreach (var content in operation.RequestBody.Content.Values)
            {
                var resolvedSchema = content.Schema.ResolveSchema();

                if (resolvedSchema is null)
                    continue;

                var matchingKey = resolvedSchema.Properties?.Keys.FirstOrDefault(k => string.Equals(k, schemaKey, StringComparison.OrdinalIgnoreCase));

                if (matchingKey is not null && resolvedSchema.Properties!.TryGetValue(matchingKey, out var propSchema))
                {
                    content.Schema = propSchema;
                    content.EnsureOperationLocalSchemaForMutation();
                    promoted = true;
                }
            }

            if (promoted && SchemaNameGenerator.GetReferenceId(requestDtoType, docOpts.ShortSchemaNames) is { } refId)
                sharedCtx.PromotedRequestWrapperSchemaRefs.TryAdd(refId, 0);

            if (promoted && fromFormProp is not null)
                NormalizePromotedFormRequestBodyContent(operation, epDef.FormDataContentType);

            // JSON Patch unwrap: only for [FromBody], promote the operations array to top-level
            if (fromBodyProp is not null && operation.RequestBody.Content.TryGetValue("application/json-patch+json", out var patchContent))
            {
                var patchArraySchema = TryGetJsonPatchArraySchema(patchContent.Schema);

                if (patchArraySchema is not null)
                {
                    patchContent.Schema = patchArraySchema;
                    patchContent.EnsureOperationLocalSchemaForMutation();
                }
            }

            return promoted ? new(schemaKey, promoteProp.PropertyType) : null;
        }

        static (PropertyInfo? Promoted, PropertyInfo? FromBody, PropertyInfo? FromForm) FindPromotedBodyProperty(Type requestDtoType)
        {
            PropertyInfo? fromBodyProp = null;
            PropertyInfo? fromFormProp = null;

            foreach (var prop in GetPublicInstanceProperties(requestDtoType))
            {
                if (fromBodyProp is null && prop.IsDefined(Types.FromBodyAttribute, false))
                    fromBodyProp = prop;

                if (fromFormProp is null && prop.IsDefined(Types.FromFormAttribute, false))
                    fromFormProp = prop;

                if (fromBodyProp is not null && fromFormProp is not null)
                    break;
            }

            return (fromBodyProp ?? fromFormProp, fromBodyProp, fromFormProp);
        }

        static void NormalizePromotedFormRequestBodyContent(OpenApiOperation operation, string? formDataContentType)
        {
            if (operation.RequestBody?.Content is not { Count: > 0 } content)
                return;

            var targetContentType = string.IsNullOrWhiteSpace(formDataContentType)
                                        ? "multipart/form-data"
                                        : formDataContentType;
            var targetKey = content.Keys.FindCaseInsensitiveKey(targetContentType);
            var targetContent = targetKey is not null
                                    ? content[targetKey]
                                    : content.Values.First();

            content.Clear();
            content[targetContentType] = targetContent;
        }

        public void ApplyParameterMetadata(OpenApiOperation operation, EndpointDefinition epDef)
        {
            if (operation.Parameters is not { Count: > 0 })
                return;

            var requestDtoType = GetRequestDtoType(epDef);
            var requestProps = requestDtoType is null ? null : GetPublicInstanceProperties(requestDtoType);
            var requestPropLookup = requestProps is null ? null : BuildRequestPropertyLookup(requestProps);
            var paramDescriptions = epDef.EndpointSummary?.Params;

            foreach (var param in operation.Parameters)
            {
                if (param is not OpenApiParameter concreteParam)
                    continue;

                var prop = concreteParam.Name is { } parameterName
                               ? FindRequestProperty(requestPropLookup, parameterName, concreteParam.In ?? ParameterLocation.Query)
                               : null;

                if (paramDescriptions is { Count: > 0 })
                {
                    var descriptionKey = prop?.Name ?? concreteParam.Name;
                    var description = descriptionKey is not null
                                          ? FindParamDescription(paramDescriptions, descriptionKey)
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

            ApplyExampleRequestToParams(operation, epDef, requestPropLookup);
        }

        public void ApplyExamples(OpenApiOperation operation, EndpointDefinition epDef, RequestTransformState state, PromotedBodyProperty? promotedBodyProperty)
        {
            if (epDef.EndpointSummary?.RequestExamples.Count is not > 0)
                return;

            if (operation.RequestBody?.Content is null)
                return;

            var examples = BuildUniqueRequestExamples(epDef.EndpointSummary.RequestExamples);
            var fallbackExample = GetRequestExampleFallback(epDef, state, promotedBodyProperty);
            var exampleNodes = new List<(RequestExample Example, JsonNode? Node)>(examples.Count);

            foreach (var example in examples)
                exampleNodes.Add((example, BuildRequestExampleNode(example.Value, state.PropsRemovedFromBody, promotedBodyProperty)));

            foreach (var content in operation.RequestBody.Content.Values)
            {
                var schema = content.Schema.ResolveSchema();

                if (exampleNodes.Count == 1)
                {
                    content.Example = NormalizeExampleNode(
                        exampleNodes[0].Node?.DeepClone(),
                        schema,
                        fallbackExample);
                    content.Examples?.Clear();
                }
                else
                {
                    content.Example = null;
                    content.Examples ??= new Dictionary<string, IOpenApiExample>();

                    foreach (var (example, exampleNode) in exampleNodes)
                    {
                        content.Examples[example.Label] = new OpenApiExample
                        {
                            Summary = example.Summary,
                            Description = example.Description,
                            Value = NormalizeExampleNode(
                                exampleNode?.DeepClone(),
                                schema,
                                fallbackExample)
                        };
                    }
                }
            }
        }

        public void ApplyParamDescriptionsToBodySchema(OpenApiOperation operation,
                                                       EndpointDefinition epDef,
                                                       RequestTransformState state,
                                                       PromotedBodyProperty? promotedBodyProperty)
        {
            if (operation.RequestBody?.Content is null)
                return;

            var paramDescriptions = epDef.EndpointSummary?.Params;
            var hasParams = paramDescriptions is { Count: > 0 };
            var exampleObj = epDef.EndpointSummary?.ExampleRequest;
            var defaultProps = BuildRequestSchemaDefaultLookup(promotedBodyProperty?.Type ?? GetRequestDtoType(epDef));
            var hasDefaults = defaultProps.Count > 0;

            if (!hasParams && exampleObj is null && !hasDefaults)
                return;

            Dictionary<string, JsonNode>? propExamples = null;
            var requestExampleNode = exampleObj is null
                                          ? null
                                          : BuildRequestExampleNode(exampleObj, state.PropsRemovedFromBody, promotedBodyProperty);
            JsonNode? fallbackExample = null;

            if (exampleObj is not null and not IEnumerable && requestExampleNode is JsonObject obj)
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
                var schema = content.Schema.ResolveSchema();

                if (schema is null)
                    continue;

                if (hasDefaults)
                    ApplyDefaultValues(schema, defaultProps);

                if (hasParams || requestExampleNode is not null)
                {
                    schema = content.EnsureOperationLocalSchemaIfShared(sharedCtx);

                    if (schema is null)
                        continue;
                }

                if (requestExampleNode is not null)
                {
                    fallbackExample ??= GetRequestExampleFallback(epDef, state, promotedBodyProperty);
                    schema.Example = NormalizeExampleNode(requestExampleNode.DeepClone(), schema, fallbackExample);
                }

                if (schema.Properties is null)
                    continue;

                foreach (var (propName, propSchema) in schema.Properties)
                {
                    if (propSchema is not OpenApiSchema concreteProp)
                        continue;

                    if (paramDescriptions is not null)
                    {
                        var description = FindParamDescription(paramDescriptions, propName);
                        if (description is not null)
                            concreteProp.Description = description;
                    }

                    if (propExamples is not null && propExamples.TryGetValue(propName, out var exVal))
                        concreteProp.Example = exVal;
                }
            }
        }

        Dictionary<string, System.ComponentModel.DefaultValueAttribute> BuildRequestSchemaDefaultLookup(Type? requestType)
        {
            if (requestType is null)
                return [];

            var defaults = new Dictionary<string, System.ComponentModel.DefaultValueAttribute>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in GetPublicInstanceProperties(requestType))
            {
                var defaultAttr = prop.GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>();

                if (defaultAttr?.Value is null)
                    continue;

                var schemaName = PropertyNameResolver.GetSchemaPropertyName(prop, NamingPolicy, docOpts.UsePropertyNamingPolicy);
                defaults[schemaName] = defaultAttr;
            }

            return defaults;
        }

        static void ApplyDefaultValues(OpenApiSchema schema, Dictionary<string, System.ComponentModel.DefaultValueAttribute> defaultProps)
        {
            if (schema.Properties is null)
                return;

            foreach (var (propName, propSchema) in schema.Properties)
            {
                if (propSchema is OpenApiSchema { Default: null } concreteProp && defaultProps.TryGetValue(propName, out var defaultAttr))
                    concreteProp.Default = defaultAttr.Value.JsonNodeFromObject();
            }
        }

        Dictionary<(ParameterLocation Location, string Name), PropertyInfo> BuildRequestPropertyLookup(PropertyInfo[] requestProps)
        {
            var lookup = new Dictionary<(ParameterLocation Location, string Name), PropertyInfo>(_parameterLookupKeyComparer);

            foreach (var prop in requestProps)
            {
                AddRequestPropertyLookup(lookup, prop, ParameterLocation.Path);
                AddRequestPropertyLookup(lookup, prop, ParameterLocation.Query);
                AddRequestPropertyLookup(lookup, prop, ParameterLocation.Header);
                AddRequestPropertyLookup(lookup, prop, ParameterLocation.Cookie);
            }

            return lookup;
        }

        void AddRequestPropertyLookup(Dictionary<(ParameterLocation Location, string Name), PropertyInfo> lookup, PropertyInfo property, ParameterLocation location)
            => lookup.TryAdd((location, GetEffectiveParameterName(property, location)), property);

        static PropertyInfo? FindRequestProperty(Dictionary<(ParameterLocation Location, string Name), PropertyInfo>? lookup, string parameterName, ParameterLocation location)
        {
            if (lookup is null)
                return null;

            return lookup.TryGetValue((location, parameterName), out var property)
                       ? property
                       : null;
        }

        string GetEffectiveParameterName(PropertyInfo property, ParameterLocation location)
        {
            if (location == ParameterLocation.Header)
                return property.GetCustomAttribute<FromHeaderAttribute>()?.HeaderName ?? property.Name.ApplyPropNamingPolicy(docOpts, NamingPolicy);

            if (location == ParameterLocation.Cookie)
                return property.GetCustomAttribute<FromCookieAttribute>()?.CookieName ?? property.Name.ApplyPropNamingPolicy(docOpts, NamingPolicy);

            if (location == ParameterLocation.Path)
            {
                return property.GetCustomAttribute<BindFromAttribute>()?.Name?.ApplyPropNamingPolicy(docOpts, NamingPolicy) ??
                       property.Name.ApplyPropNamingPolicy(docOpts, NamingPolicy);
            }

            if (location == ParameterLocation.Query)
                return GetQueryParameterName(property);

            return property.Name;
        }

        static void ApplyExampleRequestToParams(OpenApiOperation operation,
                                                 EndpointDefinition epDef,
                                                 Dictionary<(ParameterLocation Location, string Name), PropertyInfo>? requestPropLookup)
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
                {
                    concreteParam.Example = matchingProp.Value.DeepClone();

                    continue;
                }

                var prop = concreteParam.Name is { } parameterName
                               ? FindRequestProperty(requestPropLookup, parameterName, concreteParam.In ?? ParameterLocation.Query)
                               : null;
                var propValue = prop?.DeclaringType?.IsInstanceOfType(exampleRequest) is true
                                    ? prop.GetValue(exampleRequest)
                                    : null;

                if (propValue is not null)
                    concreteParam.Example = propValue.JsonNodeFromObject();
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
                operation.RemovePropFromRequestBody(p, sharedCtx, docOpts, NamingPolicy, state.PropsRemovedFromBody);
            }
        }

        static Dictionary<string, RouteParameterInfo> BuildRouteParameterLookup(List<RouteParameterInfo> routeParameters)
        {
            var lookup = new Dictionary<string, RouteParameterInfo>(routeParameters.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var routeParameter in routeParameters)
                lookup.TryAdd(routeParameter.Name, routeParameter);

            return lookup;
        }

        void AddBoundParameters(OpenApiOperation operation,
                                List<PropertyInfo> requestDtoProps,
                                Dictionary<string, RouteParameterInfo> routeParameters,
                                bool isGetRequest,
                                RequestTransformState state)
        {
            for (var i = 0; i < requestDtoProps.Count; i++)
            {
                var p = requestDtoProps[i];

                AddAttributeParameters(operation, p, state);
                AddRouteParameter(operation, p, routeParameters, state);

                var queryParamName = GetQueryParameterName(p);

                if (ShouldAddQueryParam(p, operation, queryParamName, isGetRequest && !docOpts.EnableGetRequestsWithBody))
                {
                    operation.RemovePropFromRequestBody(p, sharedCtx, docOpts, NamingPolicy, state.PropsRemovedFromBody);

                    if (TryAddComplexFromQueryParameters(operation, p, docOpts.ShortSchemaNames))
                        continue;

                    AddParameter(
                        operation,
                        queryParamName,
                        ParameterLocation.Query,
                        p,
                        GetDontBindRequiredness(p),
                        docOpts.ShortSchemaNames);
                }
            }
        }

        string GetQueryParameterName(PropertyInfo property)
            => property.GetCustomAttribute<BindFromAttribute>()?.Name ?? property.Name.ApplyPropNamingPolicy(docOpts, NamingPolicy);

        bool TryAddComplexFromQueryParameters(OpenApiOperation operation, PropertyInfo property, bool shortSchemaNames)
        {
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (!property.IsDefined(typeof(FromQueryAttribute), false) ||
                !propertyType.IsComplexType() ||
                propertyType.IsCollection() ||
                OperationSchemaHelpers.TryGetDictionaryValueType(propertyType) is not null)
                return false;

            AddComplexQueryParameters(operation, propertyType, prefix: null, shortSchemaNames, []);

            return true;
        }

        void AddComplexQueryParameters(OpenApiOperation operation,
                                       Type type,
                                       string? prefix,
                                       bool shortSchemaNames,
                                       HashSet<Type> visited)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (!visited.Add(type))
                return;

            foreach (var prop in GetBindableRequestProperties(type))
            {
                if (prop.IsDefined(Types.HideFromDocsAttribute))
                    continue;

                var propName = GetQueryParameterName(prop);
                var key = string.IsNullOrEmpty(prefix) ? propName : $"{prefix}.{propName}";
                var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                if (propType.IsComplexType() &&
                    !propType.IsCollection() &&
                    OperationSchemaHelpers.TryGetDictionaryValueType(propType) is null)
                {
                    AddComplexQueryParameters(operation, propType, key, shortSchemaNames, visited);

                    continue;
                }

                if (propType.IsCollection() && OperationSchemaHelpers.TryGetCollectionElementType(propType) is { } elementType && elementType.IsComplexType())
                {
                    AddComplexQueryParameters(operation, elementType, $"{key}[0]", shortSchemaNames, visited);

                    continue;
                }

                AddParameter(operation, key, ParameterLocation.Query, prop, GetDontBindRequiredness(prop), shortSchemaNames);
            }

            visited.Remove(type);
        }

        static bool? GetDontBindRequiredness(PropertyInfo property)
            => property.GetCustomAttribute<DontBindAttribute>()?.IsRequired is true ? true : null;

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
                            operation.RemovePropFromRequestBody(p, sharedCtx, docOpts, NamingPolicy, state.PropsRemovedFromBody);

                            continue;
                        }

                        AddParameter(operation, pName, ParameterLocation.Header, p, hAttrib.IsRequired, docOpts.ShortSchemaNames);

                        if (hAttrib.IsRequired || hAttrib.RemoveFromSchema)
                            operation.RemovePropFromRequestBody(p, sharedCtx, docOpts, NamingPolicy, state.PropsRemovedFromBody);

                        break;
                    }

                    case FromCookieAttribute cAttrib:
                    {
                        var pName = cAttrib.CookieName ?? p.Name.ApplyPropNamingPolicy(docOpts, NamingPolicy);
                        AddParameter(operation, pName, ParameterLocation.Cookie, p, cAttrib.IsRequired, docOpts.ShortSchemaNames);

                        if (cAttrib.IsRequired || cAttrib.RemoveFromSchema)
                            operation.RemovePropFromRequestBody(p, sharedCtx, docOpts, NamingPolicy, state.PropsRemovedFromBody);

                        break;
                    }

                    case FromClaimAttribute cAttrib when cAttrib.IsRequired || cAttrib.RemoveFromSchema:
                    case HasPermissionAttribute pAttrib when pAttrib.IsRequired || pAttrib.RemoveFromSchema:
                        operation.RemovePropFromRequestBody(p, sharedCtx, docOpts, NamingPolicy, state.PropsRemovedFromBody);

                        break;
                }
            }
        }

        void AddRouteParameter(OpenApiOperation operation, PropertyInfo p, Dictionary<string, RouteParameterInfo> routeParameters, RequestTransformState state)
        {
            var bindName = p.GetCustomAttribute<BindFromAttribute>()?.Name ?? p.Name;

            if (!routeParameters.TryGetValue(bindName, out var matchingRouteParam))
                return;

            operation.RemovePropFromRequestBody(p, sharedCtx, docOpts, NamingPolicy, state.PropsRemovedFromBody);

            var appliedName = matchingRouteParam.Name.GetOpenApiRouteParameterName(docOpts, NamingPolicy);

            if (TryNormalizeExistingPathParameter(operation, matchingRouteParam.Name, appliedName, p.PropertyType))
                return;

            if (!HasParameter(operation, ParameterLocation.Path, appliedName))
                AddParameter(operation, appliedName, ParameterLocation.Path, p, true, docOpts.ShortSchemaNames);
            else
                UpdateParameterSchema(operation, ParameterLocation.Path, appliedName, p.PropertyType, sharedCtx, docOpts.ShortSchemaNames);
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
                UpdateParameterSchema(operation, ParameterLocation.Path, appliedName, schemaType, sharedCtx, docOpts.ShortSchemaNames);

            return true;

            static OpenApiParameter? FindPathParameter(OpenApiOperation operation, string name)
            {
                if (operation.Parameters is not { Count: > 0 })
                    return null;

                foreach (var param in operation.Parameters)
                {
                    if (param is OpenApiParameter { In: ParameterLocation.Path, Name: not null } concreteParam &&
                        string.Equals(concreteParam.Name, name, StringComparison.OrdinalIgnoreCase))
                        return concreteParam;
                }

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
            var hasCtorDefault = prop?.GetParentCtorDefaultValue() is not null;
            var required = isRequired ?? (!hasCtorDefault && !isNullable);

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
                var example = OperationSchemaHelpers.ParseXmlExampleJsonNode(XmlDocLookup.GetPropertyExample(prop)) ??
                              propType.GenerateSampleJsonNode(NamingPolicy, docOpts.UsePropertyNamingPolicy);

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

        sealed class ParameterLookupKeyComparer : IEqualityComparer<(ParameterLocation Location, string Name)>
        {
            public bool Equals((ParameterLocation Location, string Name) x, (ParameterLocation Location, string Name) y)
                => x.Location == y.Location && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode((ParameterLocation Location, string Name) obj)
                => HashCode.Combine(obj.Location, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
        }

        static bool IsIllegalHeaderName(string name)
            => _illegalHeaderNames.Contains(name);

        static void ValidateRequestDto(EndpointDefinition epDef, Type requestDtoType, bool requestDtoIsList)
        {
            if (requestDtoType == Types.EmptyRequest || requestDtoIsList || GlobalConfig.AllowEmptyRequestDtos)
                return;

            if (GetBindableRequestProperties(requestDtoType).Any())
                return;

            throw new NotSupportedException(
                "Request DTOs without any publicly accessible properties are not supported. " +
                $"Offending Endpoint: [{epDef.EndpointType.FullName}] " +
                $"Offending DTO type: [{requestDtoType.FullName}]");
        }

        static bool ShouldAddQueryParam(PropertyInfo prop, OpenApiOperation operation, string queryParamName, bool isGetRequest)
        {
            foreach (var attribute in prop.GetCustomAttributes())
            {
                switch (attribute)
                {
                    case FromHeaderAttribute:
                    case FromCookieAttribute:
                        return false;
                    case FromClaimAttribute cAttrib:
                        return !cAttrib.IsRequired;
                    case HasPermissionAttribute pAttrib:
                        return !pAttrib.IsRequired;
                    case DontBindAttribute dontBindAttrib when dontBindAttrib.BindingSources.HasFlag(Source.QueryParam):
                        return false;
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

            return (type.IsComplexType() && !type.IsCollection()) ||
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

    }
}
