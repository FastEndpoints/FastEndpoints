using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using static FastEndpoints.OpenApi.OperationReflectionCache;
using static FastEndpoints.OpenApi.OperationTransformer;

namespace FastEndpoints.OpenApi;

sealed class RequestTransformState
{
    public HashSet<string> PropsRemovedFromBody { get; } = new(StringComparer.OrdinalIgnoreCase);
    public JsonNode? RequestBodyFallbackExample { get; set; }
    public bool RequestBodyFallbackExampleCreated { get; set; }
}

sealed record PromotedBodyProperty(string Name, Type Type);

sealed partial class RequestOperationTransformer(DocumentOptions docOpts, SharedContext sharedCtx)
{
    readonly OperationParameterFactory _parameterFactory = new(docOpts, sharedCtx);
    readonly OperationParameterNameResolver _parameterNameResolver = new(docOpts, sharedCtx);
    readonly RequestBodyOverrideApplicator _requestBodyOverrideApplicator = new(docOpts, sharedCtx);
    readonly RequestParameterMetadataApplicator _requestParameterMetadataApplicator = new(docOpts, sharedCtx);
    readonly ComplexQueryParameterExpander _complexQueryParameterExpander = new(new(docOpts, sharedCtx), new(docOpts, sharedCtx));
    readonly RouteParameterApplicator _routeParameterApplicator = new(docOpts, sharedCtx);

    static readonly HashSet<string> _illegalHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accept",
        "Content-Type",
        "Authorization"
    };

    JsonNamingPolicy? NamingPolicy => sharedCtx.NamingPolicy;
    JsonSerializerOptions SerializerOptions => sharedCtx.SerializerOptions ?? Cfg.SerOpts.Options;

    public RequestTransformState HandleParameters(OpenApiOperation operation,
                                                  OpenApiOperationTransformerContext context,
                                                  EndpointDefinition epDef,
                                                  string documentPath,
                                                  string operationKey)
    {
        var state = new RequestTransformState();
        var endpointRouteTemplate = FindEndpointRouteTemplate(epDef, documentPath);
        var routeParameters = GetRouteParameters(endpointRouteTemplate ?? context.Description.RelativePath ?? documentPath);
        var routeParameterLookup = RouteParameterApplicator.BuildLookup(routeParameters);

        var requestDtoType = epDef.ReqDtoType;

        if (requestDtoType != Types.EmptyRequest)
        {
            var isGetRequest = context.Description.HttpMethod == "GET";
            var requestDtoIsList = requestDtoType.IsCollection();
            var requestDtoProps = requestDtoIsList
                                      ? null
                                      : GetPublicInstanceProperties(requestDtoType).ToList();

            ValidateRequestDto(epDef, requestDtoType, requestDtoIsList);

            if (requestDtoProps is not null)
            {
                RemoveHiddenProperties(operation, requestDtoProps, state, operationKey);
                AddBoundParameters(operation, requestDtoProps, routeParameterLookup, isGetRequest, state, operationKey);
            }

            // remove request body if GET request (unless explicitly enabled) or if empty
            if ((isGetRequest && !docOpts.EnableGetRequestsWithBody) || operation.IsRequestBodyEmpty(sharedCtx))
            {
                if (!requestDtoIsList)
                    operation.RequestBody = null;
            }
        }

        _routeParameterApplicator.EnsureRouteParameters(operation, routeParameters);

        return state;
    }

    public PromotedBodyProperty? ApplyBodyOverrides(OpenApiOperation operation, EndpointDefinition epDef, string operationKey)
        => _requestBodyOverrideApplicator.Apply(operation, epDef, operationKey);

    public void ApplyParameterMetadata(OpenApiOperation operation, EndpointDefinition epDef)
        => _requestParameterMetadataApplicator.Apply(operation, epDef);

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
            var schema = content.Schema.ResolveSchema(sharedCtx);

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
                                                   PromotedBodyProperty? promotedBodyProperty,
                                                   string operationKey)
    {
        if (operation.RequestBody?.Content is null)
            return;

        var paramDescriptions = epDef.EndpointSummary?.Params;
        var hasParams = paramDescriptions is { Count: > 0 };
        var paramDescriptionLookup = hasParams ? paramDescriptions!.ToCaseInsensitiveDictionary(paramDescriptions!.Count) : null;
        var exampleObj = epDef.EndpointSummary?.ExampleRequest;
        var defaultProps = BuildRequestSchemaDefaultLookup(promotedBodyProperty?.Type ?? epDef.ReqDtoType);
        var hasDefaults = defaultProps.Count > 0;

        if (!hasParams && exampleObj is null && !hasDefaults)
            return;

        Dictionary<string, JsonNode>? propExamples = null;
        var requestExampleNode = exampleObj is null
                                     ? null
                                     : BuildRequestExampleNode(exampleObj, state.PropsRemovedFromBody, promotedBodyProperty);
        JsonNode? fallbackExample = null;
        var mutationCtx = new OperationSchemaMutationContext(sharedCtx, operationKey);

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
            var schema = content.Schema.ResolveSchema(sharedCtx);

            if (schema is null)
                continue;

            if (hasDefaults || hasParams || requestExampleNode is not null)
            {
                schema = content.EnsureOperationLocalSchemaForMutation(mutationCtx, "requestBody");

                if (schema is null)
                    continue;
            }

            if (hasDefaults)
                ApplyDefaultValues(schema, defaultProps, mutationCtx);

            if (requestExampleNode is not null)
            {
                fallbackExample ??= GetRequestExampleFallback(epDef, state, promotedBodyProperty);
                schema.Example = NormalizeExampleNode(requestExampleNode.DeepClone(), schema, fallbackExample);
            }

            if (schema.Properties is null)
                continue;

            foreach (var (propName, propSchema) in schema.Properties)
            {
                string? description = null;
                JsonNode? exVal = null;
                var hasDescription = paramDescriptionLookup?.TryGetValue(propName, out description) == true;
                var hasExample = propExamples?.TryGetValue(propName, out exVal) == true;

                if (!hasDescription && !hasExample)
                    continue;

                var concreteProp = propSchema.EnsureSchemaForMutation(
                    mutationCtx,
                    $"requestBody.{propName}",
                    localized => schema.Properties![propName] = localized);

                if (concreteProp is null)
                    continue;

                if (hasDescription)
                    concreteProp.Description = description;

                if (hasExample)
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
            var defaultAttr = GetPropertyMetadata(prop).DefaultValue;

            if (defaultAttr?.Value is null)
                continue;

            var schemaName = PropertyNameResolver.GetSchemaPropertyName(prop, NamingPolicy, docOpts.UsePropertyNamingPolicy);
            defaults[schemaName] = defaultAttr;
        }

        return defaults;
    }

    void ApplyDefaultValues(OpenApiSchema schema, Dictionary<string, System.ComponentModel.DefaultValueAttribute> defaultProps, OperationSchemaMutationContext mutationCtx)
    {
        if (schema.Properties is null)
            return;

        foreach (var (propName, propSchema) in schema.Properties)
        {
            if (!defaultProps.TryGetValue(propName, out var defaultAttr))
                continue;

            var concreteProp = propSchema.EnsureSchemaForMutation(
                mutationCtx,
                $"requestBody.{propName}",
                localized => schema.Properties![propName] = localized);

            if (concreteProp is { Default: null })
                concreteProp.Default = defaultAttr.ToJsonNode(SerializerOptions);
        }
    }

    void RemoveHiddenProperties(OpenApiOperation operation, List<PropertyInfo> requestDtoProps, RequestTransformState state, string operationKey)
    {
        for (var i = requestDtoProps.Count - 1; i >= 0; i--)
        {
            var p = requestDtoProps[i];

            var metadata = GetPropertyMetadata(p);

            if (!metadata.IsJsonIgnoredAlways &&
                !metadata.IsHiddenFromDocs &&
                metadata.HasPublicSetter)
                continue;

            requestDtoProps.RemoveAt(i);
            operation.RemovePropFromRequestBody(p, sharedCtx, operationKey, docOpts, NamingPolicy, state.PropsRemovedFromBody);
        }
    }

    void AddBoundParameters(OpenApiOperation operation,
                            List<PropertyInfo> requestDtoProps,
                            Dictionary<string, RouteParameterInfo> routeParameters,
                            bool isGetRequest,
                            RequestTransformState state,
                            string operationKey)
    {
        for (var i = 0; i < requestDtoProps.Count; i++)
        {
            var p = requestDtoProps[i];
            var metadata = GetPropertyMetadata(p);

            AddAttributeParameters(operation, p, metadata, state, operationKey);
            _routeParameterApplicator.AddBoundRouteParameter(operation, p, routeParameters, state, operationKey);

            var queryParamName = _parameterNameResolver.GetQueryName(p);

            if (ShouldAddQueryParam(p, metadata, operation, queryParamName, isGetRequest && !docOpts.EnableGetRequestsWithBody))
            {
                operation.RemovePropFromRequestBody(p, sharedCtx, operationKey, docOpts, NamingPolicy, state.PropsRemovedFromBody);

                if (_complexQueryParameterExpander.TryAdd(operation, p, docOpts.ShortSchemaNames))
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

    static bool? GetDontBindRequiredness(PropertyInfo property)
        => property.GetCustomAttribute<DontBindAttribute>()?.IsRequired is true ? true : null;

    void AddAttributeParameters(OpenApiOperation operation,
                                PropertyInfo p,
                                PropertyMetadata metadata,
                                RequestTransformState state,
                                string operationKey)
    {
        if (metadata.FromHeader is { } hAttrib)
        {
            var pName = hAttrib.HeaderName ?? _parameterNameResolver.ApplyPropertyNamingPolicy(p.Name);

            if (IsIllegalHeaderName(pName))
            {
                operation.RemovePropFromRequestBody(p, sharedCtx, operationKey, docOpts, NamingPolicy, state.PropsRemovedFromBody);

                return;
            }

            AddParameter(operation, pName, ParameterLocation.Header, p, hAttrib.IsRequired, docOpts.ShortSchemaNames);

            if (hAttrib.IsRequired || hAttrib.RemoveFromSchema)
                operation.RemovePropFromRequestBody(p, sharedCtx, operationKey, docOpts, NamingPolicy, state.PropsRemovedFromBody);
        }

        if (metadata.FromCookie is { } cAttrib)
        {
            var pName = cAttrib.CookieName ?? _parameterNameResolver.ApplyPropertyNamingPolicy(p.Name);
            AddParameter(operation, pName, ParameterLocation.Cookie, p, cAttrib.IsRequired, docOpts.ShortSchemaNames);

            if (cAttrib.IsRequired || cAttrib.RemoveFromSchema)
                operation.RemovePropFromRequestBody(p, sharedCtx, operationKey, docOpts, NamingPolicy, state.PropsRemovedFromBody);
        }

        if (metadata.FromClaim is { IsRequired: true } or { RemoveFromSchema: true } ||
            metadata.HasPermission is { IsRequired: true } or { RemoveFromSchema: true })
            operation.RemovePropFromRequestBody(p, sharedCtx, operationKey, docOpts, NamingPolicy, state.PropsRemovedFromBody);
    }

    void AddParameter(OpenApiOperation operation,
                      string name,
                      ParameterLocation location,
                      PropertyInfo? prop,
                      bool? isRequired = null,
                      bool shortSchemaNames = false,
                      Type? explicitType = null)
    {
        OperationParameterCollection.Add(operation, _parameterFactory.Create(name, location, prop, isRequired, shortSchemaNames, explicitType));
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

    static bool ShouldAddQueryParam(PropertyInfo prop,
                                    PropertyMetadata metadata,
                                    OpenApiOperation operation,
                                    string queryParamName,
                                    bool isGetRequest)
    {
        if (metadata.FromHeader is not null || metadata.FromCookie is not null)
            return false;

        if (metadata.FromClaim is { } fromClaim)
            return !fromClaim.IsRequired;

        if (metadata.HasPermission is { } hasPermission)
            return !hasPermission.IsRequired;

        if (metadata.DontBind?.BindingSources.HasFlag(Source.QueryParam) == true)
            return false;

        if (operation.Parameters?.Any(p => string.Equals(p.Name, queryParamName, StringComparison.OrdinalIgnoreCase)) == true)
            return false;

        return isGetRequest || prop.IsDefined(Types.QueryParamAttribute);
    }
}
