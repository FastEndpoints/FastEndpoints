using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Routing;
using Namotion.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Annotations;
using NJsonSchema.NewtonsoftJson.Generation;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;
using JsonIgnoreAttribute = System.Text.Json.Serialization.JsonIgnoreAttribute;

namespace FastEndpoints.Swagger;

sealed partial class OperationProcessor(DocumentOptions docOpts) : IOperationProcessor
{
    internal const string FERouteKey = "__fastEndpointsRoute";
    internal const string FEVersionKey = "__fastEndpointsVersion";
    internal const string FEStartingReleaseKey = "__fastEndpointsStartingRelease";
    internal const string FEDeprecatedAtKey = "__fastEndpointsDeprecatedAt";

    static readonly TextInfo _textInfo = CultureInfo.InvariantCulture.TextInfo;
    static readonly string[] _illegalHeaderNames = ["Accept", "Content-Type", "Authorization"];
    static readonly JsonSchema _x402HeaderSchema = JsonSchema.FromType<string>();

    [GeneratedRegex("(?<={)(?:.*?)*(?=})")]
    private static partial Regex RouteParamsRegex();

    [GeneratedRegex("(?<={)([^?:}]+)[^}]*(?=})")]
    private static partial Regex RouteConstraintsRegex();

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

    public bool Process(OperationProcessorContext ctx)
    {
        var state = CreateProcessingState(ctx);

        if (state is null)
            return true; //this is not a fastendpoint

        ApplyOperationMetadata(state);
        ApplyResponseMetadata(state);
        ApplyOperationDescriptions(state);
        InitializeRequestProcessing(state);
        AddRequestParameters(state, BuildRequestParameters(state));
        FinalizeRequestBody(state);
        ApplyRequestBodyOverrides(state);

        return true;
    }

    static ProcessingState? CreateProcessingState(OperationProcessorContext ctx)
    {
        var aspCtx = (AspNetCoreOperationProcessorContext)ctx;
        var metadata = aspCtx.ApiDescription.ActionDescriptor.EndpointMetadata;
        var epDef = metadata.OfType<EndpointDefinition>().SingleOrDefault(); //use shortcut `ctx.GetEndpointDefinition()` for your own processors

        return epDef is null ? null : new(ctx, aspCtx, metadata, epDef);
    }

    void ApplyOperationMetadata(ProcessingState state)
    {
        var nameMetadata = state.Metadata.OfType<EndpointNameMetadata>().LastOrDefault();

        if (nameMetadata is not null)
            state.Operation.OperationId = nameMetadata.EndpointName;

        ApplyAutoTag(state);
        ApplyFastEndpointMetadata(state);
        NormalizeRequestContentTypes(state);
    }

    void ApplyAutoTag(ProcessingState state)
    {
        if (docOpts.AutoTagPathSegmentIndex <= 0 || state.EndpointDefinition.DontAutoTagEndpoints)
            return;

        var overrideVal = state.Metadata.OfType<AutoTagOverride>().SingleOrDefault()?.TagName;
        string? tag = null;

        if (overrideVal is not null)
            tag = TagName(overrideVal, docOpts.TagCase, docOpts.TagStripSymbols);
        else
        {
            var segments = state.BareRoute.Split('/').Where(s => s != string.Empty).ToArray();
            if (segments.Length >= docOpts.AutoTagPathSegmentIndex)
                tag = TagName(segments[docOpts.AutoTagPathSegmentIndex - 1], docOpts.TagCase, docOpts.TagStripSymbols);
        }

        if (tag is not null)
            state.Operation.Tags.Add(tag);
    }

    static void ApplyFastEndpointMetadata(ProcessingState state)
    {
        //this metadata is consumed and later removed by the document processor.
        var extData = state.Operation.ExtensionData ??= new Dictionary<string, object?>();
        extData[FERouteKey] = $"{state.OpCtx.OperationDescription.Method}:{state.BareRoute}";
        extData[FEVersionKey] = state.EndpointDefinition.Version.Current;
        extData[FEStartingReleaseKey] = state.EndpointDefinition.Version.StartingReleaseVersion;
        extData[FEDeprecatedAtKey] = state.EndpointDefinition.Version.DeprecatedAt;
    }

    static void NormalizeRequestContentTypes(ProcessingState state)
    {
        if (state.RequestContent?.Count > 0)
        {
            var contentVal = state.RequestContent.FirstOrDefault().Value;
            var list = new List<KeyValuePair<string, OpenApiMediaType>>(state.Operation.Consumes.Count);
            for (var i = 0; i < state.Operation.Consumes.Count; i++)
                list.Add(new(state.Operation.Consumes[i], contentVal));
            state.RequestContent.Clear();
            foreach (var c in list)
                state.RequestContent.Add(c);
        }
    }

    static void ApplyOperationDescriptions(ProcessingState state)
    {
        state.Operation.Summary = state.EndpointDefinition.EndpointSummary?.Summary ?? state.EndpointDefinition.EndpointType.GetSummary();
        state.Operation.Description = state.EndpointDefinition.EndpointSummary?.Description ?? state.EndpointDefinition.EndpointType.GetDescription();

        if (state.EndpointDefinition.EndpointType.GetCustomAttribute<ObsoleteAttribute>() is not null)
            state.Operation.IsDeprecated = true;

        ApplyResponseDescriptions(state);
    }

    void InitializeRequestProcessing(ProcessingState state)
    {
        ApplyAspVersioningRequestParameterWorkaround(state);

        state.RequestDtoType = state.ApiDescription.ParameterDescriptions.FirstOrDefault()?.Type;
        state.RequestDtoIsList = state.RequestDtoType?.GetInterfaces().Contains(Types.IEnumerable) is true;
        state.IsGetRequest = state.ApiDescription.HttpMethod == "GET";
        state.RequestDtoProps =
            state.RequestDtoIsList
                ? null
                : state.RequestDtoType?.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy).ToList();

        ValidateRequestDto(state);

        //store unique request param description + example (from each consumes/content type) for later use.
        //todo: this is not ideal in case two consumes dtos has a prop with the same name.
        state.RequestParamDescriptions = BuildRequestParamDescriptions(state);
        ApplyRequestParamDescriptions(state);
        RemoveHiddenRequestProperties(state);
        state.ParamCtx = new(state.OpCtx, docOpts, state.Serializer, state.RequestParamDescriptions, state.ApiDescription.RelativePath!);
    }

    static void ApplyAspVersioningRequestParameterWorkaround(ProcessingState state)
    {
        if (!GlobalConfig.IsUsingAspVersioning)
            return;

        //because asp-versioning adds the version route segment as a path parameter
        for (var i = state.ApiDescription.ParameterDescriptions.Count - 1; i >= 0; i--)
        {
            if (state.ApiDescription.ParameterDescriptions[i].Source != Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Body)
                state.ApiDescription.ParameterDescriptions.RemoveAt(i);
        }
    }

    static void ValidateRequestDto(ProcessingState state)
    {
        if (state.RequestDtoType != Types.EmptyRequest && state.RequestDtoProps?.Count == 0 && !GlobalConfig.AllowEmptyRequestDtos) //see: RequestBinder.cs > static ctor
        {
            throw new NotSupportedException(
                "Request DTOs without any publicly accessible properties are not supported. " +
                $"Offending Endpoint: [{state.EndpointDefinition.EndpointType.FullName}] " +
                $"Offending DTO type: [{state.RequestDtoType!.FullName}]");
        }
    }

    void RemoveHiddenRequestProperties(ProcessingState state)
    {
        if (state.RequestDtoProps is null)
            return;

        foreach (var p in state.RequestDtoProps.Where(
                                   p => p.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition == JsonIgnoreCondition.Always ||
                                        p.IsDefined(Types.HideFromDocsAttribute) ||
                                        p.GetSetMethod()?.IsPublic is not true)
                               .ToArray())
        {
            RemovePropFromRequestBodyContent(p.Name, state, docOpts);
            state.RequestDtoProps.Remove(p);
        }
    }

    List<OpenApiParameter> BuildRequestParameters(ProcessingState state)
    {
        var requestParams = CreateRouteParameters(state);

        AddQueryParameters(state, requestParams);
        AddAttributeBasedParameters(state, requestParams);
        AddSwagger2FileParameters(state, requestParams);
        AddIdempotencyHeader(state, requestParams);
        AddX402Header(state, requestParams);

        return requestParams;
    }

    List<OpenApiParameter> CreateRouteParameters(ProcessingState state)
    {
        return RouteParamsRegex()
               .Matches(state.OperationPath)
               .Select(
                   m =>
                   {
                       var pInfo = state.RequestDtoProps?.SingleOrDefault(
                           p =>
                           {
                               var pName = p.GetCustomAttribute<BindFromAttribute>()?.Name ?? p.Name;

                               if (!string.Equals(pName, m.Value, StringComparison.OrdinalIgnoreCase))
                                   return false;

                               //need to match complete segments including parenthesis:
                               //https://github.com/FastEndpoints/FastEndpoints/issues/709
                               state.OperationPath = state.OperationPath.Replace(
                                   $"{{{m.Value}}}",
                                   $"{{{m.Value.ApplyPropNamingPolicy(docOpts)}}}");

                               RemovePropFromRequestBodyContent(p.Name, state, docOpts);

                               return true;
                           });

                       return CreateParam(state.ParamCtx, OpenApiParameterKind.Path, pInfo, m.Value, true);
                   })
               .ToList();
    }

    void AddQueryParameters(ProcessingState state, List<OpenApiParameter> requestParams)
    {
        if (state.RequestDtoType is null)
            return;

        var queryParams = state.RequestDtoProps?
                               .Where(
                                   p => ShouldAddQueryParam(
                                       p,
                                       requestParams,
                                       state.IsGetRequest && !docOpts.EnableGetRequestsWithBody,
                                       docOpts)) //user wants body in GET requests
                               .Select(
                                   p =>
                                   {
                                       RemovePropFromRequestBodyContent(p.Name, state, docOpts);

                                       return CreateParam(state.ParamCtx, OpenApiParameterKind.Query, p);
                                   })
                               .ToList();

        if (queryParams?.Count > 0)
            requestParams.AddRange(queryParams);
    }

    void AddAttributeBasedParameters(ProcessingState state, List<OpenApiParameter> requestParams)
    {
        if (state.RequestDtoProps is null)
            return;

        foreach (var p in state.RequestDtoProps)
        {
            foreach (var attribute in p.GetCustomAttributes())
            {
                switch (attribute)
                {
                    case FromHeaderAttribute hAttrib:
                    {
                        var pName = hAttrib.HeaderName ?? p.Name;

                        if (_illegalHeaderNames.Any(n => n.Equals(pName, StringComparison.OrdinalIgnoreCase)))
                        {
                            RemovePropFromRequestBodyContent(p.Name, state, docOpts);

                            continue;
                        }

                        requestParams.Add(CreateParam(state.ParamCtx, OpenApiParameterKind.Header, p, pName, hAttrib.IsRequired));

                        if (hAttrib.IsRequired || hAttrib.RemoveFromSchema)
                            RemovePropFromRequestBodyContent(p.Name, state, docOpts);

                        break;
                    }

                    case FromCookieAttribute cAttrib:
                    {
                        var pName = cAttrib.CookieName ?? p.Name;

                        requestParams.Add(CreateParam(state.ParamCtx, OpenApiParameterKind.Cookie, p, pName, cAttrib.IsRequired));

                        if (cAttrib.IsRequired || cAttrib.RemoveFromSchema)
                            RemovePropFromRequestBodyContent(p.Name, state, docOpts);

                        break;
                    }

                    case FromClaimAttribute cAttrib when cAttrib.IsRequired || cAttrib.RemoveFromSchema:
                    case HasPermissionAttribute pAttrib when pAttrib.IsRequired || pAttrib.RemoveFromSchema:
                        RemovePropFromRequestBodyContent(p.Name, state, docOpts);

                        break;
                }
            }
        }
    }

    void AddSwagger2FileParameters(ProcessingState state, List<OpenApiParameter> requestParams)
    {
        if (!state.OpCtx.IsSwagger2() || state.RequestDtoProps is null)
            return;

        foreach (var p in state.RequestDtoProps.ToArray())
        {
            if (p.PropertyType != Types.IFormFile)
                continue;

            RemovePropFromRequestBodyContent(p.Name, state, docOpts);
            state.RequestDtoProps.Remove(p);
            requestParams.Add(CreateParam(state.ParamCtx, OpenApiParameterKind.FormData, p));
        }
    }

    static void AddIdempotencyHeader(ProcessingState state, List<OpenApiParameter> requestParams)
    {
        if (state.EndpointDefinition.IdempotencyOptions is null)
            return;

        var prm = CreateParam(state.ParamCtx, OpenApiParameterKind.Header, null, state.EndpointDefinition.IdempotencyOptions.HeaderName, true);
        prm.Example = state.EndpointDefinition.IdempotencyOptions.SwaggerExampleGenerator?.Invoke();
        prm.Description = state.EndpointDefinition.IdempotencyOptions.SwaggerHeaderDescription;
        if (state.EndpointDefinition.IdempotencyOptions.SwaggerHeaderType is not null)
            prm.Schema = JsonSchema.FromType(state.EndpointDefinition.IdempotencyOptions.SwaggerHeaderType);
        requestParams.Add(prm);
    }

    static void AddX402Header(ProcessingState state, List<OpenApiParameter> requestParams)
    {
        if (state.EndpointDefinition.X402PaymentMetadata is null)
            return;

        var prm = CreateParam(state.ParamCtx, OpenApiParameterKind.Header, null, X402Constants.PaymentSignatureHeader, false);
        prm.Name = X402Constants.PaymentSignatureHeader;
        prm.Description = "Base64-encoded x402 payment payload.";
        prm.Schema = _x402HeaderSchema;
        requestParams.Add(prm);
    }

    static void AddRequestParameters(ProcessingState state, List<OpenApiParameter> requestParams)
    {
        foreach (var p in requestParams)
        {
            if (GlobalConfig.IsUsingAspVersioning)
            {
                //remove any duplicate params - ref: https://github.com/FastEndpoints/FastEndpoints/issues/560
                for (var i = state.Operation.Parameters.Count - 1; i >= 0; i--)
                {
                    var prm = state.Operation.Parameters[i];
                    if (prm.Name == p.Name && prm.Kind == p.Kind)
                        state.Operation.Parameters.RemoveAt(i);
                }
            }

            state.Operation.Parameters.Add(p);
        }
    }

    void FinalizeRequestBody(ProcessingState state)
    {
        //remove request body if this is a GET request (swagger ui/fetch client doesn't support GET with body).
        //note: user can decide to allow GET requests with body via EnableGetRequestsWithBody setting.
        //or if there are no properties left on the request dto after above operations.
        //only if the request dto is not a list.
        if ((state.IsGetRequest && !docOpts.EnableGetRequestsWithBody) || state.RequestContent?.HasNoProperties() is true)
        {
            if (!state.RequestDtoIsList)
            {
                state.Operation.RequestBody = null;

                for (var i = state.Operation.Parameters.Count - 1; i >= 0; i--)
                {
                    if (state.Operation.Parameters[i].Kind == OpenApiParameterKind.Body)
                        state.Operation.Parameters.RemoveAt(i);
                }
            }
        }

        if (docOpts.RemoveEmptyRequestSchema)
            RemoveEmptyRequestSchemas(state.OpCtx.Document.Components.Schemas);
    }

    static void ApplyRequestBodyOverrides(ProcessingState state)
    {
        var fromBodyProp = state.RequestDtoProps?.FirstOrDefault(p => p.IsDefined(Types.FromBodyAttribute, false));
        var fromFormProp = state.RequestDtoProps?.FirstOrDefault(p => p.IsDefined(Types.FromFormAttribute, false));
        ReplaceRequestBodyFromProperty(state, fromBodyProp, true);
        ReplaceRequestBodyFromProperty(state, fromFormProp, false);
        ApplyRequestExamples(state, fromBodyProp, fromFormProp);
    }

    static JsonSchema? TryGetJsonPatchArraySchema(JsonSchema? schema)
    {
        if (schema?.ActualSchema.IsObject is not true)
            return null;

        var props = schema.ActualSchema.ActualProperties;

        if (props.Count != 1)
            return null;

        var operationsProp = props.FirstOrDefault(p => string.Equals(p.Key, "operations", StringComparison.OrdinalIgnoreCase)).Value;

        return operationsProp?.ActualSchema.IsArray is true
                   ? operationsProp.ActualSchema
                   : null;
    }

    static bool ShouldAddQueryParam(PropertyInfo prop, List<OpenApiParameter> reqParams, bool isGetRequest, DocumentOptions docOpts)
    {
        var paramName = prop.Name.ApplyPropNamingPolicy(docOpts);

        foreach (var attribute in prop.GetCustomAttributes())
        {
            switch (attribute)
            {
                case BindFromAttribute bAtt:
                    paramName = bAtt.Name;

                    break;
                case FromHeaderAttribute:
                    return false; // because header request params are being added
                case FromClaimAttribute cAttrib:
                    return !cAttrib.IsRequired; // add param if it's not required. if required only can bind from actual claim.
                case HasPermissionAttribute pAttrib:
                    return !pAttrib.IsRequired; // add param if it's not required. if required only can bind from actual permission.
            }
        }

        return

            //it's a GET request and request params already has it. so don't add
            (isGetRequest && !reqParams.Any(rp => rp.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase))) ||

            //this prop is marked with [QueryParam], so add. applies to all verbs.
            prop.IsDefined(Types.QueryParamAttribute);
    }

    static void RemovePropFromRequestBodyContent(string propName, ProcessingState state, DocumentOptions docOpts)
    {
        if (state.RequestContent is null)
            return;

        propName = propName.ApplyPropNamingPolicy(docOpts);

        state.PropsToRemoveFromExample.Add(propName);

        foreach (var c in state.RequestContent)
        {
            var key = c.GetAllProperties()
                       .FirstOrDefault(p => string.Equals(p.Key, propName, StringComparison.OrdinalIgnoreCase))
                       .Key;
            Remove(c.Value.Schema.ActualSchema, key);
        }

        //recursive property removal
        static void Remove(JsonSchema schema, string? key)
        {
            if (key is null)
                return;

            schema.Properties.Remove(key);

            //because validation schema processor may have added this prop/key, which should be removed when the prop is being removed from the schema
            schema.RequiredProperties.Remove(key);

            foreach (var s in schema.AllOf.Union(schema.AllInheritedSchemas))
                Remove(s, key);
        }
    }

    static string StripRouteConstraints(string relativePath)
    {
        var parts = relativePath.Split('/');

        for (var i = 0; i < parts.Length; i++)
            parts[i] = RouteConstraintsRegex().Replace(parts[i], "$1");

        return string.Join("/", parts);
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

    static OpenApiParameter CreateParam(ParamCreationContext ctx,
                                        OpenApiParameterKind kind,
                                        PropertyInfo? prop = null,
                                        string? paramName = null,
                                        bool? isRequired = null)
    {
        paramName = paramName?.ApplyPropNamingPolicy(ctx.DocOpts) ??
                    prop?.GetCustomAttribute<BindFromAttribute>()?.Name ?? //don't apply naming policy to attribute value
                    prop?.Name.ApplyPropNamingPolicy(ctx.DocOpts) ?? throw new InvalidOperationException("param name is required!");

        var typeOverrideAttr = prop?.GetCustomAttribute<JsonSchemaTypeAttribute>();

        var propType = typeOverrideAttr?.Type ??         //attribute gets first priority
                       prop?.PropertyType ??             //property type gets second priority
                       ctx.TypeForRouteParam(paramName); //use route constraint map as last resort

        if (propType.Name.EndsWith("HeaderValue"))
            propType = Types.String;

        var prm = ctx.OpCtx.DocumentGenerator.CreatePrimitiveParameter(
            paramName,
            ctx.Descriptions.GetValueOrDefault(prop?.Name ?? paramName)?.Description,
            propType.ToContextualType());

        prm.Kind = kind;

        var defaultValFromCtorArg = prop?.GetParentCtorDefaultValue();
        bool? hasDefaultValFromCtorArg = null;
        if (defaultValFromCtorArg is not null)
            hasDefaultValFromCtorArg = true;

        var isNullable = typeOverrideAttr?.IsNullable ?? prop?.IsNullable();

        prm.IsRequired = isRequired ??
                         !hasDefaultValFromCtorArg ??
                         !(isNullable ?? true);

        if (ctx.OpCtx.IsSwagger2() && prm.Schema is null)
        {
            prm.Schema = JsonSchema.FromType(propType);
            prm.Schema.Title = null;
        }

        //fix enums not rendering as dropdowns in swagger ui due to nswag bug
        if (isNullable is true && Nullable.GetUnderlyingType(propType)?.IsEnum is true && prm.Schema.OneOf.Count == 1)
        {
            prm.Schema.AllOf.Add(prm.Schema.OneOf.Single());
            prm.Schema.OneOf.Clear();
        }
        else if (propType.IsEnum && prm.Schema.Reference?.IsEnumeration is true)
        {
            prm.Schema.AllOf.Add(new() { Reference = prm.Schema.ActualSchema });
            prm.Schema.Reference = null;
        }

        prm.Schema.IsNullableRaw = prm.IsRequired ? null : isNullable;

        if (kind == OpenApiParameterKind.Body &&
            prm.Schema.OneOf.SingleOrDefault()?.Reference?.IsObject is true &&
            prm.Schema.OneOf.Single().Reference?.Discriminator is null)
        {
            prm.Schema = prm.Schema.OneOf.Single();
            prm.Schema.OneOf.Clear();
        }

        if (ctx.OpCtx.IsSwagger2())
            prm.Default = prop?.GetCustomAttribute<DefaultValueAttribute>()?.Value ?? defaultValFromCtorArg;
        else
            prm.Schema.Default = prop?.GetCustomAttribute<DefaultValueAttribute>()?.Value ?? defaultValFromCtorArg;

        if (ctx.OpCtx.Settings.SchemaSettings.GenerateExamples)
        {
            if (ctx.Descriptions.TryGetValue(prop?.Name ?? prm.Name, out var desc) && desc.Example is not null)
                prm.Example = desc.Example;
            else
                prm.Example = prop?.GetExampleJToken(ctx.Serializer);

            if (prm.Example is null && prm.Default is null && prm.Schema?.Default is null && prm.IsRequired)
            {
                var jToken = prm.ActualSchema.ToSampleJson();
                prm.Example = jToken.HasValues ? jToken : null;
            }
        }

        prm.IsNullableRaw = null; //if this is not null, nswag generates an incorrect swagger spec for some unknown reason.

        return prm;
    }

    void ApplyResponseMetadata(ProcessingState state)
    {
        if (state.Operation.Responses.Count == 0)
            return;

        var responseMetas = BuildResponseMetadata(state);

        if (responseMetas.Count == 0)
            return;

        ApplyIResultResponseWorkaround(state.OpCtx, state.Operation, responseMetas);

        foreach (var rsp in state.Operation.Responses)
        {
            var mediaType = rsp.Value.Content.FirstOrDefault().Value;

            if (responseMetas.TryGetValue(rsp.Key, out var meta))
            {
                if (mediaType is not null && meta.Example is not null)
                    mediaType.Example = meta.Example;

                AddResponseHeaders(state, rsp.Value, meta);

                if (mediaType is not null)
                    ReplaceResponseContentTypes(rsp.Value, mediaType, meta.ContentTypes);
            }

            if (docOpts.UseOneOfForPolymorphism)
                FixResponsePolymorphism(rsp.Value);

            FixBinaryResponseSchemas(rsp.Value);
            AddX402ResponseHeaders(state.EndpointDefinition, rsp.Key, rsp.Value);
        }
    }

    static Dictionary<string, ResponseMeta> BuildResponseMetadata(ProcessingState state)
    {
        return state.Metadata
                    .OfType<IProducesResponseTypeMetadata>()
                    .GroupBy(
                        m => m.StatusCode,
                        (k, g) =>
                        {
                            var meta = g.Last();
                            object? example = null;
                            _ = state.EndpointDefinition.EndpointSummary?.ResponseExamples.TryGetValue(k, out example);
                            example = meta.GetExampleFromMetaData() ?? example;
                            example = example is not null ? JToken.FromObject(example, state.Serializer) : null;

                            if (state.OpCtx.IsSwagger2() && example is JToken { Type: JTokenType.Array } token)
                                example = token.ToString();

                            return new ResponseMeta(
                                k.ToString(),
                                [..meta.ContentTypes],
                                example,
                                state.EndpointDefinition.EndpointSummary?.ResponseHeaders.Where(h => h.StatusCode == k).ToArray(),
                                meta.Type,
                                meta.Type is not null && Types.IResult.IsAssignableFrom(meta.Type)); //todo: remove when .net 9 sdk bug is fixed
                        })
                    .ToDictionary(x => x.Key);
    }

    static void ApplyIResultResponseWorkaround(OperationProcessorContext ctx, OpenApiOperation op, Dictionary<string, ResponseMeta> responseMetas)
    {
    #if NET9_0_OR_GREATER
        //remove this workaround when sdk bug is fixed: https://github.com/dotnet/aspnetcore/issues/57801#issuecomment-2439578287
        foreach (var meta in responseMetas.Values.Where(m => m.IsIResult && m.DtoType is not null))
        {
            var contentType = meta.ContentTypes.FirstOrDefault();

            if (contentType is null)
                continue;

            var res = new OpenApiResponse { Content = { [contentType] = new() { Schema = new() } } };

            if (!ctx.SchemaResolver.HasSchema(meta.DtoType!, false))
            {
                var schema = ctx.SchemaGenerator.Generate(meta.DtoType!, ctx.SchemaResolver);
                ctx.SchemaResolver.AppendSchema(schema, schema.Title);
                res.Schema.Reference = schema;
            }
            else
                res.Schema.Reference = ctx.SchemaResolver.GetSchema(meta.DtoType!, false);

            op.Responses[meta.Key] = res;
            var orderedResponses = op.Responses.OrderBy(kvp => kvp.Key).ToArray();
            op.Responses.Clear();

            foreach (var rsp in orderedResponses)
                op.Responses.Add(rsp);
        }
    #endif
    }

    void AddResponseHeaders(ProcessingState state, OpenApiResponse response, ResponseMeta meta)
    {
        if (meta.DtoType is not null)
        {
            foreach (var p in meta.DtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                                  .Where(p => p.IsDefined(Types.ToHeaderAttribute)))
            {
                var headerName = p.GetCustomAttribute<ToHeaderAttribute>()?.HeaderName ?? p.Name.ApplyPropNamingPolicy(docOpts);
                var summaryTag = p.GetXmlDocsSummary();
                var schema = state.OpCtx.SchemaGenerator.Generate(p.PropertyType);
                response.Headers[headerName] = new()
                {
                    Description = summaryTag,
                    Example = p.GetExampleJToken(state.Serializer) ?? schema.ToSampleJson(),
                    Schema = schema
                };
            }
        }

        if (meta.UserHeaders?.Length > 0)
        {
            foreach (var hdr in meta.UserHeaders)
            {
                response.Headers[hdr.HeaderName] = new()
                {
                    Description = hdr.Description,
                    Example = hdr.Example is not null ? JToken.FromObject(hdr.Example, state.Serializer) : null,
                    Schema = hdr.Example is not null ? state.OpCtx.SchemaGenerator.Generate(hdr.Example.GetType()) : null
                };
            }
        }
    }

    static void ReplaceResponseContentTypes(OpenApiResponse response, OpenApiMediaType mediaType, IReadOnlyCollection<string> contentTypes)
    {
        response.Content.Clear();

        foreach (var ct in contentTypes)
            response.Content.Add(new(ct, mediaType));
    }

    static void FixResponsePolymorphism(OpenApiResponse response)
    {
        foreach (var mt in response.Content)
        {
            if (mt.Value.Schema.ActualSchema.DiscriminatorObject?.Mapping.Count > 0 &&
                mt.Value.Schema.ActualSchema.OneOf.Count > 0)
            {
                foreach (var derived in mt.Value.Schema.ActualSchema.OneOf)
                    mt.Value.Schema.OneOf.Add(derived);

                mt.Value.Schema.Reference = null;
            }
        }
    }

    static void FixBinaryResponseSchemas(OpenApiResponse response)
    {
        foreach (var content in response.Content.Values)
        {
            if (content.Schema is { Type: JsonObjectType.String, Format: "byte" })
                content.Schema.Format = "binary";
        }
    }

    static void AddX402ResponseHeaders(EndpointDefinition epDef, string responseKey, OpenApiResponse response)
    {
        if (epDef.X402PaymentMetadata is null)
            return;

        if (responseKey == "402")
        {
            response.Headers[X402Constants.PaymentRequiredHeader] = new()
            {
                Description = "Base64-encoded x402 payment challenge payload.",
                Schema = _x402HeaderSchema
            };
        }

        response.Headers[X402Constants.PaymentResponseHeader] = new()
        {
            Description = "Base64-encoded x402 settlement result. Present when the middleware attempts settlement.",
            Schema = _x402HeaderSchema
        };
    }

    static void ApplyResponseDescriptions(ProcessingState state)
    {
        foreach (var oaResp in state.Operation.Responses.Where(r => string.IsNullOrWhiteSpace(r.Value.Description)).ToArray())
        {
            if (_defaultDescriptions.TryGetValue(oaResp.Key, out var description))
                oaResp.Value.Description = description;

            var statusCode = Convert.ToInt32(oaResp.Key);

            if (state.EndpointDefinition.EndpointSummary?.Responses.ContainsKey(statusCode) is true)
                oaResp.Value.Description = state.EndpointDefinition.EndpointSummary.Responses[statusCode];

            if (state.EndpointDefinition.EndpointSummary?.ResponseParams.ContainsKey(statusCode) is not true || oaResp.Value.Schema is null)
                continue;

            var propDescriptions = state.EndpointDefinition.EndpointSummary.ResponseParams[statusCode];
            var respDtoProps = state.ApiDescription
                                    .SupportedResponseTypes
                                    .SingleOrDefault(x => x.StatusCode == statusCode)?
                                    .Type?
                                    .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                                    .Select(
                                        p => new
                                        {
                                            key = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name,
                                            val = p.Name
                                        })
                                    .Where(x => x.key is not null)
                                    .ToDictionary(x => x.key!, x => x.val);

            foreach (var prop in oaResp.GetAllProperties())
            {
                string? propName = null;
                respDtoProps?.TryGetValue(prop.Key, out propName);
                propName ??= prop.Key;

                if (propDescriptions.TryGetValue(propName, out var responseDescription))
                    prop.Value.Description = responseDescription;
            }
        }
    }

    static void RemoveEmptyRequestSchemas(IDictionary<string, JsonSchema> schemas)
    {
        foreach (var key in schemas.Where(s => s.Value.ActualProperties.Count == 0 && s.Value.IsObject).Select(s => s.Key).ToArray())
            schemas.Remove(key);
    }

    static Dictionary<string, ParamDescription> BuildRequestParamDescriptions(ProcessingState state)
    {
        var descriptions = new Dictionary<string, ParamDescription>(StringComparer.OrdinalIgnoreCase);

        if (state.RequestContent is not null)
        {
            foreach (var c in state.RequestContent)
            {
                foreach (var prop in c.GetAllProperties())
                {
                    descriptions[prop.Key] = new(
                        prop.Value.Description,
                        prop.Value.Example != null ? JToken.FromObject(prop.Value.Example, state.Serializer) : null);
                }
            }
        }

        if (state.EndpointDefinition.EndpointSummary is not null)
        {
            foreach (var param in state.EndpointDefinition.EndpointSummary.Params)
                descriptions.GetOrAdd(param.Key, new()).Description = param.Value;
        }

        if (state.EndpointDefinition.EndpointSummary?.RequestExamples.Count is > 0)
        {
            var example = state.EndpointDefinition.EndpointSummary.RequestExamples.First().Value;

            if (example is not IEnumerable)
            {
                var jToken = JToken.FromObject(example, state.Serializer);

                foreach (var prop in jToken)
                {
                    var p = (JProperty)prop;
                    descriptions.GetOrAdd(p.Name, new()).Example = p.Value;
                }
            }
        }

        return descriptions;
    }

    static void ApplyRequestParamDescriptions(ProcessingState state)
    {
        if (state.RequestContent is null)
            return;

        foreach (var c in state.RequestContent)
        {
            foreach (var prop in c.GetAllRequestProperties())
            {
                if (!state.RequestParamDescriptions.TryGetValue(prop.Key, out var x))
                    continue;

                prop.Value.Description = x.Description;
                prop.Value.Example = x.Example;
            }
        }
    }

    static void ReplaceRequestBodyFromProperty(ProcessingState state, PropertyInfo? bodyProp, bool isJsonPatchAware)
    {
        var body = state.Operation.Parameters.FirstOrDefault(x => x.Kind == OpenApiParameterKind.Body);

        if (bodyProp is null || body is null || state.Operation.RequestBody is null)
            return;

        var oldBodyName = state.Operation.RequestBody.Name;
        var bodyParam = CreateParam(state.ParamCtx, OpenApiParameterKind.Body, bodyProp, bodyProp.Name, true);

        //otherwise xml docs from properties won't be considered due to existence of a schema level example generated from prm.ActualSchema.ToSampleJson()
        bodyParam.Example = null;

        state.Operation.RequestBody.Content.FirstOrDefault().Value.Schema = bodyParam.Schema;

        if (isJsonPatchAware && state.Operation.RequestBody.Content.TryGetValue("application/json-patch+json", out var patchContent))
            patchContent.Schema = TryGetJsonPatchArraySchema(bodyParam.Schema) ?? TryGetJsonPatchArraySchema(patchContent.Schema) ?? bodyParam.Schema;

        state.Operation.RequestBody.IsRequired = bodyParam.IsRequired;
        state.Operation.RequestBody.Description = bodyParam.Description;
        state.Operation.RequestBody.Name = bodyParam.Name;
        state.Operation.RequestBody.Position = null;
        state.OpCtx.Document.Components.Schemas.Remove(oldBodyName);
    }

    static void ApplyRequestExamples(ProcessingState state, PropertyInfo? fromBodyProp, PropertyInfo? fromFormProp)
    {
        var summary = state.EndpointDefinition.EndpointSummary;

        if (summary?.RequestExamples.Count is not > 0)
            return;

        var requestExamples = BuildUniqueRequestExamples(summary.RequestExamples);

        foreach (var requestBody in state.Operation.Parameters.Where(x => x.Kind == OpenApiParameterKind.Body))
        {
            var exCount = requestExamples.Count;

            if (exCount == 1)
            {
                requestBody.ActualSchema.Example = GetExampleObjectFrom(requestExamples[0]);

                continue;
            }

            foreach (var example in requestExamples)
            {
                var firstContent = state.RequestContent?.FirstOrDefault().Value;

                if (firstContent is null)
                    continue;

                firstContent.Examples.Add(
                    key: example.Label,
                    value: new()
                    {
                        Summary = example.Summary,
                        Description = example.Description,
                        Value = GetExampleObjectFrom(example)
                    });
            }
        }

        object? GetExampleObjectFrom(RequestExample? requestExample)
        {
            if (requestExample is null)
                return null;

            var input = requestExample.Value;
            var tInput = input.GetType();

            if (fromBodyProp is not null)
            {
                var pFromBody = tInput.GetProperty(fromBodyProp.Name);
                input = pFromBody?.GetValue(input) ?? input;
                tInput = input.GetType();
            }

            if (fromFormProp is not null)
            {
                var pFromForm = tInput.GetProperty(fromFormProp.Name);
                input = pFromForm?.GetValue(input) ?? input;
                tInput = input.GetType();
            }

            if (tInput.IsAssignableTo(typeof(IEnumerable)))
                return JToken.FromObject(input, state.Serializer);

            var example = JObject.FromObject(input, state.Serializer);

            foreach (var p in example.Properties().ToArray())
            {
                if (state.PropsToRemoveFromExample.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                    p.Remove();
            }

            return example;
        }

        static List<RequestExample> BuildUniqueRequestExamples(ICollection<RequestExample> examples)
        {
            var result = examples.ToList();

            foreach (var group in result.GroupBy(e => e.Label).Where(g => g.Count() > 1))
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

    sealed class ProcessingState
    {
        public OperationProcessorContext OpCtx { get; }
        public IEnumerable<object> Metadata { get; }
        public EndpointDefinition EndpointDefinition { get; }
        public ApiDescription ApiDescription { get; }
        public OpenApiOperation Operation { get; }
        public JsonSerializer Serializer { get; }
        public IDictionary<string, OpenApiMediaType>? RequestContent { get; }
        public string BareRoute { get; }

        public string OperationPath
        {
            get => OpCtx.OperationDescription.Path;
            set => OpCtx.OperationDescription.Path = value;
        }

        public Type? RequestDtoType { get; set; }
        public bool RequestDtoIsList { get; set; }
        public bool IsGetRequest { get; set; }
        public List<PropertyInfo>? RequestDtoProps { get; set; }
        public Dictionary<string, ParamDescription> RequestParamDescriptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public ParamCreationContext ParamCtx { get; set; }
        public List<string> PropsToRemoveFromExample { get; } = [];

        public ProcessingState(OperationProcessorContext opCtx,
                               AspNetCoreOperationProcessorContext aspNetCoreCtx,
                               IEnumerable<object> metadata,
                               EndpointDefinition endpointDefinition)
        {
            OpCtx = opCtx;
            Metadata = metadata;
            EndpointDefinition = endpointDefinition;
            ApiDescription = aspNetCoreCtx.ApiDescription;
            Operation = opCtx.OperationDescription.Operation;
            OperationPath = $"/{StripRouteConstraints(ApiDescription.RelativePath!.TrimStart('~').TrimEnd('/'))}";

            var version = $"/{GlobalConfig.VersioningPrefix ?? "v"}{EndpointDefinition.Version.Current}";
            var routePrefix = "/" + (GlobalConfig.EndpointRoutePrefix ?? "_");
            BareRoute = OperationPath.Remove(routePrefix).Remove(version);
            RequestContent = Operation.RequestBody?.Content;

            var serializerSettings = ((NewtonsoftJsonSchemaGeneratorSettings)opCtx.SchemaGenerator.Settings).SerializerSettings;
            Serializer = JsonSerializer.Create(serializerSettings);
        }
    }

    // ReSharper disable once ArrangeTypeMemberModifiers
    internal readonly partial struct ParamCreationContext
    {
        public OperationProcessorContext OpCtx { get; }
        public DocumentOptions DocOpts { get; }
        public JsonSerializer Serializer { get; }
        public Dictionary<string, ParamDescription> Descriptions { get; } //key: property name

        readonly Dictionary<string, Type> _paramMap;

        public ParamCreationContext(OperationProcessorContext opCtx,
                                    DocumentOptions docOpts,
                                    JsonSerializer serializer,
                                    Dictionary<string, ParamDescription> descriptions,
                                    string operationPath)
        {
            OpCtx = opCtx;
            DocOpts = docOpts;
            Serializer = serializer;
            Descriptions = descriptions;
            _paramMap = new(
                operationPath.Split('/')
                             .Where(s => MyRegex().IsMatch(s)) //include: api/{id:int:min(5)}:deactivate
                             .Select(
                                 s =>
                                 {
                                     var withoutBraces = s[(s.IndexOf('{') + 1)..s.IndexOfAny(['(', '}'])];
                                     var parts = withoutBraces.Split(':');
                                     var name = parts[0].Trim();
                                     var type = parts[1].Trim();

                                     GlobalConfig.RouteConstraintMap.TryGetValue(type, out var tParam);

                                     return new KeyValuePair<string, Type>(name, tParam ?? Types.String);
                                 }));
        }

        public Type TypeForRouteParam(string paramName)
            => _paramMap.TryGetValue(paramName, out var tParam)
                   ? tParam
                   : Types.String;

        //search min 1 `:` character between any `{` and `}` characters
        [GeneratedRegex(@"\{[^{}]*:[^{}]*\}")]
        private static partial Regex MyRegex();
    }

    sealed class ResponseMeta(string key, IReadOnlyCollection<string> contentTypes, object? example, ResponseHeader[]? userHeaders, Type? dtoType, bool isIResult)
    {
        public string Key { get; } = key;
        public IReadOnlyCollection<string> ContentTypes { get; } = contentTypes;
        public object? Example { get; } = example;
        public ResponseHeader[]? UserHeaders { get; } = userHeaders;
        public Type? DtoType { get; } = dtoType;
        public bool IsIResult { get; } = isIResult;
    }
}

sealed class ParamDescription(string? description = null, JToken? example = null)
{
    public string? Description { get; set; } = description;
    public JToken? Example { get; set; } = example;
}