using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed partial class OperationTransformer(DocumentOptions docOpts, SharedContext sharedCtx) : IOpenApiOperationTransformer
{
    const BindingFlags PublicInstanceHierarchy = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
    static readonly TextInfo _textInfo = CultureInfo.InvariantCulture.TextInfo;
    static readonly string[] _illegalHeaderNames = ["Accept", "Content-Type", "Authorization"];
    static readonly ConcurrentDictionary<Type, TypeMetadata> _typeMetadataCache = new();

    sealed class TypeMetadata
    {
        public required PropertyInfo[] PublicInstanceProperties { get; init; }
        public Dictionary<string, string>? JsonNameMap { get; init; }
    }

    [GeneratedRegex(@"(?<=\{)[^}]+(?=\})")]
    private static partial Regex RouteParamsRegex();

    [GeneratedRegex("(?<={)([^?:}]+)[^}]*(?=})")]
    private static partial Regex RouteConstraintsRegex();

    sealed class RouteParameterInfo
    {
        public required string Name { get; init; }
        public Type? ConstraintType { get; init; }
    }

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

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var epDef = metadata.OfType<EndpointDefinition>().SingleOrDefault();

        docOpts.Services ??= context.ApplicationServices;

        // compute the document path for this operation
        var relativePath = context.Description.RelativePath?.TrimStart('~').TrimEnd('/') ?? "";
        var documentPath = "/" + StripRouteConstraints(relativePath);
        var httpMethod = context.Description.HttpMethod?.ToUpperInvariant() ?? "GET";
        var operationKey = $"{httpMethod}:{documentPath}";

        if (epDef is null)
        {
            if (docOpts.ExcludeNonFastEndpoints)
                return Task.CompletedTask;

            // not a FastEndpoint
            sharedCtx.Operations[operationKey] = new()
            {
                OperationKey = operationKey,
                DocumentPath = documentPath,
                HttpMethod = httpMethod,
                Version = 0,
                StartingReleaseVersion = 0,
                DeprecatedAt = 0,
                IsFastEndpoint = false
            };

            return Task.CompletedTask;
        }

        // apply endpoint filter
        if (docOpts.EndpointFilter?.Invoke(epDef) == false)
            return Task.CompletedTask;

        // store version metadata for document transformer
        var version = $"/{GlobalConfig.VersioningPrefix ?? "v"}{epDef.Version.Current}";
        var routePrefix = "/" + (GlobalConfig.EndpointRoutePrefix ?? "_");
        var bareRoute = documentPath.Remove(routePrefix).Remove(version);
        var bareKey = $"{httpMethod}:{bareRoute}";

        sharedCtx.Operations[operationKey] = new()
        {
            OperationKey = bareKey,
            DocumentPath = documentPath,
            HttpMethod = httpMethod,
            Version = epDef.Version.Current,
            StartingReleaseVersion = epDef.Version.StartingReleaseVersion,
            DeprecatedAt = epDef.Version.DeprecatedAt,
            IsFastEndpoint = true
        };

        // operation ID
        var nameMetadata = metadata.OfType<EndpointNameMetadata>().LastOrDefault();
        if (nameMetadata is not null)
            operation.OperationId = nameMetadata.EndpointName;

        // auto-tagging
        ApplyAutoTag(operation, epDef, bareRoute, metadata);

        // summary / description
        operation.Summary ??= epDef.EndpointSummary?.Summary;
        operation.Description ??= epDef.EndpointSummary?.Description;

        // deprecation from [Obsolete]
        if (epDef.EndpointType.GetCustomAttribute<ObsoleteAttribute>() is not null)
            operation.Deprecated = true;

        // handle request parameters
        var propsRemovedFromBody = HandleRequestParameters(operation, context, epDef, documentPath);

        // handle [FromBody]/[FromForm] request body replacement + JSON Patch unwrap
        ApplyRequestBodyOverrides(operation, epDef);

        // apply parameter descriptions from EndpointSummary.Params and defaults from [DefaultValue]
        ApplyParameterMetadata(operation, epDef);

        // add missing responses from IProducesResponseTypeMetadata that ApiExplorer may have skipped
        // (e.g., 400 ErrorResponse with application/problem+json content type)
        AddMissingResponses(operation, metadata);

        // handle response descriptions
        ApplyResponseDescriptions(operation, epDef, context);

        // fix binary response formats (MS OpenApi generates "byte" instead of "binary" for raw binary content types)
        FixBinaryResponseFormats(operation);

        // handle response examples from EndpointSummary
        ApplyResponseExamples(operation, epDef);

        // handle request body examples from EndpointSummary.RequestExamples
        ApplyRequestExamples(operation, epDef, propsRemovedFromBody);

        // apply EndpointSummary.Params descriptions to request body schema properties
        ApplyParamDescriptionsToRequestBodySchema(operation, epDef, propsRemovedFromBody);

        // handle response headers ([ToHeader] on response DTO + EndpointSummary.ResponseHeaders)
        AddResponseHeaders(operation, epDef, metadata);

        // fix response polymorphism if enabled
        if (docOpts.UseOneOfForPolymorphism)
            FixResponsePolymorphism(operation);

        // handle idempotency header
        AddIdempotencyHeader(operation, epDef);

        // handle x402 headers
        AddX402Headers(operation, epDef);

        // handle security requirements
        ApplySecurityRequirements(operation, epDef, metadata, docOpts, sharedCtx, operationKey);

        // drop duplicate parameters introduced by Asp.Versioning (it adds the version route
        // segment as an extra path parameter alongside the one we derive from the endpoint).
        // ref: https://github.com/FastEndpoints/FastEndpoints/issues/560
        if (GlobalConfig.IsUsingAspVersioning)
            RemoveDuplicateParameters(operation);

        // sort parameters: path, query, header, cookie
        SortParameters(operation);

        return Task.CompletedTask;
    }

    void ApplyAutoTag(OpenApiOperation operation, EndpointDefinition epDef, string bareRoute, IList<object> metadata)
    {
        // collect the tag values that came from explicit .WithTags(...) metadata so we never drop them
        var explicitTags = metadata.OfType<ITagsMetadata>()
                                   .SelectMany(t => t.Tags)
                                   .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // always strip framework-generated tags (controller/assembly name) that weren't set via WithTags,
        // regardless of whether auto-tagging is enabled. old NSwag integration never emitted them and
        // users expect the new lib to behave the same.
        if (operation.Tags is { Count: > 0 })
        {
            foreach (var t in operation.Tags.ToArray())
            {
                if (t.Name is null || !explicitTags.Contains(t.Name))
                    operation.Tags.Remove(t);
            }
        }

        if (docOpts.AutoTagPathSegmentIndex <= 0 || epDef.DontAutoTagEndpoints)
            return;

        var overrideVal = metadata.OfType<AutoTagOverride>().SingleOrDefault()?.TagName;
        string? tag = null;

        if (overrideVal is not null)
            tag = TagName(overrideVal, docOpts.TagCase, docOpts.TagStripSymbols);
        else
        {
            var segments = bareRoute.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= docOpts.AutoTagPathSegmentIndex)
                tag = TagName(segments[docOpts.AutoTagPathSegmentIndex - 1], docOpts.TagCase, docOpts.TagStripSymbols);
        }

        if (tag is not null)
        {
            operation.Tags ??= new HashSet<OpenApiTagReference>();
            operation.Tags.Add(new(tag));
        }
    }

    HashSet<string> HandleRequestParameters(OpenApiOperation operation,
                                            OpenApiOperationTransformerContext context,
                                            EndpointDefinition epDef,
                                            string documentPath)
    {
        var propsRemovedFromBody = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var endpointRouteTemplate = FindEndpointRouteTemplate(epDef, documentPath);
        var routeParameters = GetRouteParameters(endpointRouteTemplate ?? context.Description.RelativePath ?? documentPath);
        var routeParamNames = routeParameters.Select(p => p.Name).ToList();

        var requestDtoType = context.Description.ParameterDescriptions.FirstOrDefault()?.Type;

        if (requestDtoType is not null)
        {
            var isGetRequest = context.Description.HttpMethod == "GET";
            var requestDtoIsList = requestDtoType.IsCollection();
            var requestDtoProps = requestDtoIsList
                                      ? null
                                      : GetPublicInstanceProperties(requestDtoType).ToList();

            if (requestDtoProps is not null)
            {
                // remove hidden properties
                var propsToRemove = requestDtoProps
                                    .Where(
                                        p => p.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition == JsonIgnoreCondition.Always ||
                                             p.IsDefined(Types.HideFromDocsAttribute) ||
                                             p.GetSetMethod()?.IsPublic is not true)
                                    .ToArray();

                foreach (var p in propsToRemove)
                {
                    requestDtoProps.Remove(p);
                    operation.RemovePropFromRequestBody(p.Name, propsRemovedFromBody);
                }

                // handle attribute-based parameters
                foreach (var p in requestDtoProps.ToArray())
                {
                    foreach (var attribute in p.GetCustomAttributes())
                    {
                        switch (attribute)
                        {
                            case FromHeaderAttribute hAttrib:
                            {
                                var pName = hAttrib.HeaderName ?? p.Name.ApplyPropNamingPolicy(docOpts);

                                if (_illegalHeaderNames.Any(n => n.Equals(pName, StringComparison.OrdinalIgnoreCase)))
                                {
                                    operation.RemovePropFromRequestBody(p.Name, propsRemovedFromBody);

                                    continue;
                                }

                                AddParameter(operation, pName, ParameterLocation.Header, p, hAttrib.IsRequired, docOpts.ShortSchemaNames);

                                if (hAttrib.IsRequired || hAttrib.RemoveFromSchema)
                                    operation.RemovePropFromRequestBody(p.Name, propsRemovedFromBody);

                                break;
                            }

                            case FromCookieAttribute cAttrib:
                            {
                                var pName = cAttrib.CookieName ?? p.Name.ApplyPropNamingPolicy(docOpts);
                                AddParameter(operation, pName, ParameterLocation.Cookie, p, cAttrib.IsRequired, docOpts.ShortSchemaNames);

                                if (cAttrib.IsRequired || cAttrib.RemoveFromSchema)
                                    operation.RemovePropFromRequestBody(p.Name, propsRemovedFromBody);

                                break;
                            }

                            case FromClaimAttribute cAttrib when cAttrib.IsRequired || cAttrib.RemoveFromSchema:
                            case HasPermissionAttribute pAttrib when pAttrib.IsRequired || pAttrib.RemoveFromSchema:
                                operation.RemovePropFromRequestBody(p.Name, propsRemovedFromBody);

                                break;
                        }
                    }

                    // handle route params - remove from body and add as path parameter if not already present
                    var bindName = p.GetCustomAttribute<BindFromAttribute>()?.Name ?? p.Name;
                    var matchingRouteParam = routeParamNames.FirstOrDefault(rp => string.Equals(rp, bindName, StringComparison.OrdinalIgnoreCase));

                    if (matchingRouteParam is not null)
                    {
                        operation.RemovePropFromRequestBody(p.Name, propsRemovedFromBody);

                        // add as path parameter using the route template variable name with naming policy applied
                        var appliedName = matchingRouteParam.ApplyPropNamingPolicy(docOpts);

                        if (!HasParameter(operation, ParameterLocation.Path, appliedName))
                            AddParameter(operation, appliedName, ParameterLocation.Path, p, true, docOpts.ShortSchemaNames);
                        else
                            UpdateParameterSchema(operation, ParameterLocation.Path, appliedName, p.PropertyType, docOpts.ShortSchemaNames);
                    }

                    // handle query params
                    if (ShouldAddQueryParam(p, operation, isGetRequest && !docOpts.EnableGetRequestsWithBody))
                    {
                        operation.RemovePropFromRequestBody(p.Name, propsRemovedFromBody);
                        AddParameter(operation, p.Name.ApplyPropNamingPolicy(docOpts), ParameterLocation.Query, p, shortSchemaNames: docOpts.ShortSchemaNames);
                    }
                }
            }

            // remove request body if GET request (unless explicitly enabled) or if empty
            if ((isGetRequest && !docOpts.EnableGetRequestsWithBody) || operation.IsRequestBodyEmpty())
            {
                if (!requestDtoIsList)
                    operation.RequestBody = null;
            }
        }

        // ensure all route template params have path parameters
        // (handles endpoints without request DTOs and route params without matching DTO properties)
        for (var i = 0; i < routeParamNames.Count; i++)
        {
            var routeParam = routeParamNames[i];
            var appliedName = routeParam.ApplyPropNamingPolicy(docOpts);
            var resolvedType = routeParameters[i].ConstraintType;

            if (HasParameter(operation, ParameterLocation.Path, appliedName))
            {
                if (resolvedType is not null)
                    UpdateParameterSchema(operation, ParameterLocation.Path, appliedName, resolvedType, docOpts.ShortSchemaNames);

                continue;
            }

            // resolve the route constraint (e.g. "{id:int}") via GlobalConfig.RouteConstraintMap so
            // orphan route params get a typed schema instead of defaulting to string
            AddParameter(operation, appliedName, ParameterLocation.Path, null, true, docOpts.ShortSchemaNames, resolvedType);
        }

        // apply naming policy to any framework-generated path params that weren't already handled
        if (operation.Parameters is { Count: > 0 })
        {
            foreach (var param in operation.Parameters)
            {
                if (param is OpenApiParameter { In: ParameterLocation.Path, Name: not null } concreteParam)
                {
                    var renamed = concreteParam.Name.ApplyPropNamingPolicy(docOpts);
                    if (renamed != concreteParam.Name)
                        concreteParam.Name = renamed;
                }
            }
        }

        return propsRemovedFromBody;
    }

    static bool ShouldAddQueryParam(PropertyInfo prop, OpenApiOperation operation, bool isGetRequest)
    {
        foreach (var attribute in prop.GetCustomAttributes())
        {
            switch (attribute)
            {
                case BindFromAttribute:
                    return false;
                case FromHeaderAttribute:
                    return false;
                case FromClaimAttribute cAttrib:
                    return !cAttrib.IsRequired;
                case HasPermissionAttribute pAttrib:
                    return !pAttrib.IsRequired;
            }
        }

        // already exists as a parameter (e.g. route param, cookie param)
        if (operation.Parameters?.Any(p => string.Equals(p.Name, prop.Name, StringComparison.OrdinalIgnoreCase)) == true)
            return false;

        return isGetRequest || prop.IsDefined(Types.QueryParamAttribute);
    }

    static void AddParameter(OpenApiOperation operation,
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

        var schema = propType.GetSchemaForType(shortSchemaNames);
        var isNullable = prop?.IsNullable() ?? false;
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

        // set parameter example: XML doc <example> tag takes priority, then auto-generated sample
        if (required && prop?.PropertyType is not null)
        {
            JsonNode? example = null;

            // XML doc <example> tag gets first priority
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

            // fall back to auto-generated sample (use coerced propType, not original PropertyType)
            example ??= propType.GenerateSampleJsonNode();

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

    static bool ShouldUseJsonParameterContent(ParameterLocation location, Type type)
    {
        if (location != ParameterLocation.Query)
            return false;

        type = Nullable.GetUnderlyingType(type) ?? type;

        return type.IsComplexType() && !type.IsCollection() ||
               OperationSchemaHelpers.TryGetDictionaryValueType(type) is not null;
    }

    static void ApplyRequestBodyOverrides(OpenApiOperation operation, EndpointDefinition epDef)
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

        // replace the entire request body schema with the [FromBody]/[FromForm] property's type schema
        foreach (var content in operation.RequestBody.Content.Values)
        {
            var resolvedSchema = content.Schema.ResolveSchema();

            if (resolvedSchema is null)
                continue;

            // find the promoted property in the schema
            var propName = promoteProp.Name;
            var policy = Extensions.NamingPolicy;
            var schemaKey = policy?.ConvertName(propName) ?? propName;

            var matchingKey = resolvedSchema.Properties?.Keys
                                            .FirstOrDefault(k => string.Equals(k, schemaKey, StringComparison.OrdinalIgnoreCase));

            if (matchingKey is not null && resolvedSchema.Properties!.TryGetValue(matchingKey, out var propSchema))
                content.Schema = propSchema;
        }

        // JSON Patch unwrap: only for [FromBody], promote the operations array to top-level
        if (fromBodyProp is not null && operation.RequestBody.Content.TryGetValue("application/json-patch+json", out var patchContent))
        {
            var patchArraySchema = TryGetJsonPatchArraySchema(patchContent.Schema);

            if (patchArraySchema is not null)
                patchContent.Schema = patchArraySchema;
        }
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

    void ApplyResponseDescriptions(OpenApiOperation operation, EndpointDefinition epDef, OpenApiOperationTransformerContext context)
    {
        if (operation.Responses is null)
            return;

        foreach (var (statusCode, response) in operation.Responses)
        {
            ApplyDefaultResponseDescription(statusCode, response);

            if (epDef.EndpointSummary is not null)
            {
                var code = int.TryParse(statusCode, out var c) ? c : 0;
                if (epDef.EndpointSummary.Responses.TryGetValue(code, out var customDesc))
                    response.Description = customDesc;

                // apply ResponseParams property descriptions to response schema properties
                if (epDef.EndpointSummary.ResponseParams.TryGetValue(code, out var propDescriptions))
                    ApplyResponseParamDescriptions(response, propDescriptions, context, code);
            }
        }
    }

    static void ApplyDefaultResponseDescription(string statusCode, IOpenApiResponse response)
    {
        if (_defaultDescriptions.TryGetValue(statusCode, out var description) &&
            (string.IsNullOrWhiteSpace(response.Description) || IsFrameworkDefault(statusCode, response.Description)))
            response.Description = description;
    }

    static void FixBinaryResponseFormats(OpenApiOperation operation)
    {
        if (operation.Responses is null)
            return;

        foreach (var response in operation.Responses.Values)
        {
            if (response is not OpenApiResponse concreteResp || concreteResp.Content is not { Count: > 0 })
                continue;

            foreach (var (contentType, mediaType) in concreteResp.Content)
            {
                // skip JSON content types — "byte" is correct for base64-encoded JSON string responses
                if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (mediaType.Schema is OpenApiSchema { Type: JsonSchemaType.String, Format: "byte" } schema)
                    schema.Format = "binary";
            }
        }
    }

    static void ApplyResponseParamDescriptions(IOpenApiResponse response,
                                               Dictionary<string, string> propDescriptions,
                                               OpenApiOperationTransformerContext context,
                                               int statusCode)
    {
        if (response is not OpenApiResponse concreteResp || concreteResp.Content is not { Count: > 0 })
            return;

        var respDtoType = context.Description.SupportedResponseTypes
                                 .SingleOrDefault(x => x.StatusCode == statusCode)?
                                 .Type;

        var jsonNameToClrName = BuildJsonNameMap(respDtoType);

        foreach (var content in concreteResp.Content.Values)
        {
            var schema = content.Schema.ResolveSchema();

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

    static Dictionary<string, string>? BuildJsonNameMap(Type? type)
    {
        return type is null ? null : GetTypeMetadata(type).JsonNameMap;
    }

    void ApplyParameterMetadata(OpenApiOperation operation, EndpointDefinition epDef)
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
                           ? requestProps?.FirstOrDefault(p => MatchesParameterName(p, parameterName))
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

    static bool MatchesParameterName(PropertyInfo property, string parameterName)
    {
        var headerAttr = property.GetCustomAttribute<FromHeaderAttribute>();

        if (headerAttr is not null)
            return string.Equals(headerAttr.HeaderName ?? property.Name, parameterName, StringComparison.OrdinalIgnoreCase);

        var bindAttr = property.GetCustomAttribute<BindFromAttribute>();

        if (bindAttr is not null)
            return string.Equals(bindAttr.Name, parameterName, StringComparison.OrdinalIgnoreCase);

        return string.Equals(property.Name, parameterName, StringComparison.OrdinalIgnoreCase);
    }

    static string? FindParamDescription(IReadOnlyDictionary<string, string> paramDescriptions, string key)
    {
        var matchingKey = paramDescriptions.Keys.FindCaseInsensitiveKey(key);

        return matchingKey is not null ? paramDescriptions[matchingKey] : null;
    }

    static PropertyInfo[] GetPublicInstanceProperties(Type type)
        => GetTypeMetadata(type).PublicInstanceProperties;

    static TypeMetadata GetTypeMetadata(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        return _typeMetadataCache.GetOrAdd(type, static t =>
        {
            var properties = t.GetProperties(PublicInstanceHierarchy);
            Dictionary<string, string>? jsonNameMap = null;

            foreach (var property in properties)
            {
                var jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;

                if (jsonName is null)
                    continue;

                jsonNameMap ??= [];
                jsonNameMap[jsonName] = property.Name;
            }

            return new()
            {
                PublicInstanceProperties = properties,
                JsonNameMap = jsonNameMap
            };
        });
    }

    static Type? GetRequestDtoType(EndpointDefinition epDef)
        => epDef.ReqDtoType;

    static bool HasParameter(OpenApiOperation operation, ParameterLocation location, string name)
        => operation.Parameters?.Any(
               p => p.In == location &&
                    string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) ==
           true;

    static void UpdateParameterSchema(OpenApiOperation operation, ParameterLocation location, string name, Type type, bool shortSchemaNames)
    {
        if (operation.Parameters is not { Count: > 0 })
            return;

        foreach (var param in operation.Parameters)
        {
            if (param.In != location || !string.Equals(param.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (param is OpenApiParameter concreteParam)
                concreteParam.Schema = type.GetSchemaForType(shortSchemaNames);

            return;
        }
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

    static void ApplyResponseExamples(OpenApiOperation operation, EndpointDefinition epDef)
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
                content.Example = exampleNode;

                // clear any framework-populated named examples to avoid invalid spec (OAS 3.1 forbids both)
                content.Examples?.Clear();
            }
        }
    }

    static void ApplyRequestExamples(OpenApiOperation operation, EndpointDefinition epDef, HashSet<string> propsRemovedFromBody)
    {
        if (epDef.EndpointSummary?.RequestExamples.Count is not > 0)
            return;

        if (operation.RequestBody?.Content is null)
            return;

        var examples = BuildUniqueRequestExamples(epDef.EndpointSummary.RequestExamples);
        var fallbackExample = BuildRequestExampleFallback(epDef, propsRemovedFromBody);

        foreach (var content in operation.RequestBody.Content.Values)
        {
            var schema = content.Schema.ResolveSchema();

            if (examples.Count == 1)
            {
                content.Example = NormalizeExampleNode(StripRemovedProps(examples[0].Value.JsonNodeFromObject(), propsRemovedFromBody), schema, fallbackExample);
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
                        Value = NormalizeExampleNode(StripRemovedProps(example.Value.JsonNodeFromObject(), propsRemovedFromBody), schema, fallbackExample)
                    };
                }
            }
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
    }

    static JsonNode? BuildRequestExampleFallback(EndpointDefinition epDef, HashSet<string> propsRemovedFromBody)
    {
        var fallback = GetRequestDtoType(epDef)?.GenerateSampleJsonNode();

        if (fallback is JsonObject fallbackObj && propsRemovedFromBody.Count > 0)
            fallbackObj.RemoveProperties(propsRemovedFromBody);

        return fallback;
    }

    void AddResponseHeaders(OpenApiOperation operation, EndpointDefinition epDef, IList<object> metadata)
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

    void AddTypedResponseHeaders(OpenApiResponse response, Type responseType)
    {
        foreach (var prop in GetPublicInstanceProperties(responseType))
        {
            var toHeaderAttr = prop.GetCustomAttribute<ToHeaderAttribute>();

            if (toHeaderAttr is null)
                continue;

            var headerName = toHeaderAttr.HeaderName ?? prop.Name.ApplyPropNamingPolicy(docOpts);

            AddResponseHeader(
                response,
                headerName,
                new()
                {
                    Schema = prop.PropertyType.GetSchemaForType(docOpts.ShortSchemaNames),
                    Example = prop.PropertyType.GetSampleValue().JsonNodeFromObject()
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

    IOpenApiSchema? CreateConfiguredResponseHeaderSchema(object? exampleValue, JsonNode? exampleNode)
    {
        if (exampleValue is null)
            return null;

        var exampleType = exampleValue.GetType();

        if (!IsAnonymousType(exampleType))
            return exampleType.GetSchemaForType(docOpts.ShortSchemaNames);

        return CreateSchemaFromExampleNode(exampleNode);
    }

    static OpenApiSchema? CreateSchemaFromExampleNode(JsonNode? node)
        => node switch
        {
            JsonObject obj => new()
            {
                Type = JsonSchemaType.Object,
                Properties = obj.ToDictionary(
                    kvp => kvp.Key,
                    IOpenApiSchema (kvp) => CreateSchemaFromExampleNode(kvp.Value) ?? OperationSchemaHelpers.StringSchema())
            },
            JsonArray arr => new()
            {
                Type = JsonSchemaType.Array,
                Items = CreateSchemaFromExampleNode(arr.FirstOrDefault()) ?? OperationSchemaHelpers.StringSchema()
            },
            JsonValue value => CreatePrimitiveSchemaFromValue(value),
            _ => null
        };

    static OpenApiSchema CreatePrimitiveSchemaFromValue(JsonValue value)
    {
        if (value.TryGetValue<bool>(out _))
            return new() { Type = JsonSchemaType.Boolean };

        if (value.TryGetValue<int>(out _))
            return new() { Type = JsonSchemaType.Integer, Format = "int32" };

        if (value.TryGetValue<long>(out _))
            return new() { Type = JsonSchemaType.Integer, Format = "int64" };

        if (value.TryGetValue<decimal>(out _))
            return new() { Type = JsonSchemaType.Number };

        return OperationSchemaHelpers.StringSchema();
    }

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

    void AddMissingResponses(OpenApiOperation operation, IList<object> metadata)
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

    void AddMissingResponseContent(OpenApiResponse response, IProducesResponseTypeMetadata metadata)
    {
        if (metadata.Type is null || metadata.Type == Types.Void || !metadata.ContentTypes.Any())
            return;

        var schemaRefId = SchemaNameGenerator.GetReferenceId(metadata.Type, docOpts.ShortSchemaNames);

        if (schemaRefId is null)
            return;

        sharedCtx.MissingSchemaTypes.TryAdd(schemaRefId, metadata.Type);
        response.Content ??= new Dictionary<string, OpenApiMediaType>();

        foreach (var contentType in metadata.ContentTypes)
        {
            if (!response.Content.ContainsKey(contentType))
                response.Content[contentType] = CreateSchemaRefMediaType(schemaRefId);
        }
    }

    static OpenApiMediaType CreateSchemaRefMediaType(string schemaRefId)
        => new()
        {
            Schema = new OpenApiSchemaReference(schemaRefId)
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

    void AddIdempotencyHeader(OpenApiOperation operation, EndpointDefinition epDef)
    {
        if (epDef.IdempotencyOptions is null)
            return;

        operation.Parameters ??= [];
        var exampleValue = epDef.IdempotencyOptions.SwaggerExampleGenerator?.Invoke();
        var exampleNode = exampleValue.JsonNodeFromObject();

        var param = new OpenApiParameter
        {
            Name = epDef.IdempotencyOptions.HeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description = epDef.IdempotencyOptions.SwaggerHeaderDescription,
            Schema = epDef.IdempotencyOptions.SwaggerHeaderType is not null
                         ? epDef.IdempotencyOptions.SwaggerHeaderType.GetSchemaForType(docOpts.ShortSchemaNames)
                         : CreateSchemaFromExampleNode(exampleNode) ?? OperationSchemaHelpers.StringSchema(),
            Example = exampleNode
        };

        operation.Parameters.Add(param);
    }

    static void AddX402Headers(OpenApiOperation operation, EndpointDefinition epDef)
    {
        if (epDef.X402PaymentMetadata is null)
            return;

        // request header
        operation.Parameters ??= [];
        operation.Parameters.Add(
            new OpenApiParameter
            {
                Name = X402Constants.PaymentSignatureHeader,
                In = ParameterLocation.Header,
                Required = false,
                Description = "Base64-encoded x402 payment payload.",
                Schema = OperationSchemaHelpers.StringSchema()
            });

        // response headers
        if (operation.Responses is not null)
        {
            foreach (var (statusCode, response) in operation.Responses)
            {
                if (response is not OpenApiResponse concreteResponse)
                    continue;

                concreteResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();

                if (statusCode == "402")
                {
                    concreteResponse.Headers[X402Constants.PaymentRequiredHeader] = new OpenApiHeader
                    {
                        Description = "Base64-encoded x402 payment challenge payload.",
                        Schema = OperationSchemaHelpers.StringSchema()
                    };
                }

                concreteResponse.Headers[X402Constants.PaymentResponseHeader] = new OpenApiHeader
                {
                    Description = "Base64-encoded x402 settlement result. Present when the middleware attempts settlement.",
                    Schema = OperationSchemaHelpers.StringSchema()
                };
            }
        }
    }

    static void ApplySecurityRequirements(OpenApiOperation operation,
                                          EndpointDefinition epDef,
                                          IList<object> metadata,
                                          DocumentOptions docOpts,
                                          SharedContext sharedCtx,
                                          string operationKey)
    {
        var authorizeAttributes = metadata.OfType<AuthorizeAttribute>().ToList();

        if (IsAnonymousOperation(metadata, authorizeAttributes))
        {
            operation.Security?.Clear();

            return;
        }

        var scopes = BuildScopes(authorizeAttributes).ToList();
        var securityEntries = BuildSecurityEntries(epDef, docOpts, scopes);

        if (securityEntries.Count > 0)
            sharedCtx.SecurityRequirements[operationKey] = securityEntries;
    }

    static bool IsAnonymousOperation(IList<object> metadata, List<AuthorizeAttribute> authorizeAttributes)
        => metadata.OfType<AllowAnonymousAttribute>().Any() || authorizeAttributes.Count == 0;

    static List<(string SchemeName, List<string> Scopes)> BuildSecurityEntries(EndpointDefinition epDef,
                                                                                DocumentOptions docOpts,
                                                                                List<string> scopes)
    {
        var securityEntries = new List<(string SchemeName, List<string> Scopes)>();

        foreach (var authConfig in docOpts.AuthSchemes)
        {
            var epSchemes = epDef.AuthSchemeNames;

            if (epSchemes?.Contains(authConfig.Name) == false)
                continue;

            securityEntries.Add((authConfig.Name, scopes));
        }

        return securityEntries;
    }

    static IEnumerable<string> BuildScopes(IEnumerable<AuthorizeAttribute> authorizeAttributes)
        => authorizeAttributes
           .Where(a => a.Roles != null)
           .SelectMany(a => a.Roles!.Split(','))
           .Distinct();

    static string StripRouteConstraints(string relativePath)
    {
        if (!relativePath.Contains('{'))
            return relativePath;

        return RouteConstraintsRegex().Replace(relativePath, "$1");
    }

    static string? FindEndpointRouteTemplate(EndpointDefinition epDef, string documentPath)
    {
        if (epDef.Routes.Length == 0)
            return null;

        if (epDef.Routes.Length == 1)
            return epDef.Routes[0];

        foreach (var route in epDef.Routes)
        {
            var finalRoute = FastEndpoints.MainExtensions.BuildRoute(new StringBuilder(), epDef.Version.Current, route, epDef.OverriddenRoutePrefix);

            if (string.Equals(NormalizeRoutePath(finalRoute), documentPath, StringComparison.OrdinalIgnoreCase))
                return route;
        }

        return null;
    }

    static string NormalizeRoutePath(string route)
    {
        route = StripRouteConstraints(route.TrimStart('~').TrimEnd('/'));

        return route.StartsWith('/') ? route : "/" + route;
    }

    static List<RouteParameterInfo> GetRouteParameters(string? relativePath)
    {
        return RouteParamsRegex()
              .Matches(relativePath ?? string.Empty)
              .Select(
                  m => new RouteParameterInfo
                  {
                      Name = GetRouteParameterName(m.Value),
                      ConstraintType = m.Value.TryResolveRouteConstraintType()
                  })
              .ToList();

        static string GetRouteParameterName(string segment)
        {
            var colonIdx = segment.IndexOf(':');
            var name = colonIdx >= 0 ? segment[..colonIdx] : segment;
            return name.TrimStart('*').TrimEnd('?');
        }
    }

    static string TagName(string input, TagCase tagCase, bool stripSymbols)
    {
        return StripSymbols(
            tagCase switch
            {
                TagCase.None => input,
                TagCase.TitleCase => _textInfo.ToTitleCase(input),
                TagCase.LowerCase => _textInfo.ToLower(input),
                _ => input
            });

        string StripSymbols(string val)
            => stripSymbols ? Regex.Replace(val, "[^a-zA-Z0-9]", "") : val;
    }

    static void ApplyParamDescriptionsToRequestBodySchema(OpenApiOperation operation, EndpointDefinition epDef, HashSet<string> propsRemovedFromBody)
    {
        if (operation.RequestBody?.Content is null)
            return;

        var hasParams = epDef.EndpointSummary?.Params is { Count: > 0 };
        var exampleObj = epDef.EndpointSummary?.ExampleRequest;
        var fallbackExample = BuildRequestExampleFallback(epDef, propsRemovedFromBody);

        if (!hasParams && exampleObj is null)
            return;

        // serialize example object properties for per-property examples
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
            var schema = content.Schema.ResolveSchema();

            if (schema is null)
                continue;

            // set schema-level example from ExampleRequest
            if (exampleObj is not null)
            {
                var exNode = exampleObj.JsonNodeFromObject();

                if (exNode is not null)
                {
                    // strip properties that were removed from the request body (claim-bound, route-bound, etc.)
                    if (propsRemovedFromBody.Count > 0 && exNode is JsonObject exObj)
                        exObj.RemoveProperties(propsRemovedFromBody);

                    schema.Example = NormalizeExampleNode(exNode, schema, fallbackExample);
                }
            }

            if (schema.Properties is null)
                continue;

            foreach (var (propName, propSchema) in schema.Properties)
            {
                if (propSchema is not OpenApiSchema concreteProp)
                    continue;

                if (hasParams)
                {
                    if (epDef.EndpointSummary?.Params != null)
                    {
                        var description = FindParamDescription(epDef.EndpointSummary.Params, propName);
                        if (description is not null)
                            concreteProp.Description = description;
                    }
                }

                if (propExamples is not null &&
                    propExamples.TryGetValue(propName, out var exVal))
                    concreteProp.Example = exVal;
            }
        }
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

    static void FixResponsePolymorphism(OpenApiOperation operation)
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

                // copy oneOf entries from the referenced schema to the response schema level
                mediaType.Schema = new OpenApiSchema { OneOf = [..actualSchema.OneOf] };
            }
        }
    }

    static void RemoveDuplicateParameters(OpenApiOperation operation)
    {
        if (operation.Parameters is not { Count: > 1 })
            return;

        var seen = new HashSet<(string Name, ParameterLocation Location)>();

        for (var i = operation.Parameters.Count - 1; i >= 0; i--)
        {
            var p = operation.Parameters[i];

            if (p.Name is null || p.In is not { } loc)
                continue;

            if (!seen.Add((p.Name, loc)))
                operation.Parameters.RemoveAt(i);
        }
    }

    static void SortParameters(OpenApiOperation operation)
    {
        if (operation.Parameters is not { Count: > 1 })
            return;

        var sorted = operation.Parameters
                              .OrderBy(
                                  p => p.In switch
                                  {
                                      ParameterLocation.Path => 0,
                                      ParameterLocation.Query => 1,
                                      ParameterLocation.Header => 2,
                                      ParameterLocation.Cookie => 3,
                                      _ => 4
                                  })
                              .ToList();

        operation.Parameters.Clear();

        foreach (var p in sorted)
            operation.Parameters.Add(p);
    }
}

file static class OperationTransformerExtensions
{
    internal static string ApplyPropNamingPolicy(this string paramName, DocumentOptions documentOptions)
    {
        if (!documentOptions.UsePropertyNamingPolicy)
            return paramName;

        var policy = Extensions.NamingPolicy;

        return policy?.ConvertName(paramName) ?? paramName;
    }

    internal static bool IsNullable(this PropertyInfo prop)
        => _nullabilityCtx.Create(prop).WriteState is NullabilityState.Nullable;

    static readonly NullabilityInfoContext _nullabilityCtx = new();
}
