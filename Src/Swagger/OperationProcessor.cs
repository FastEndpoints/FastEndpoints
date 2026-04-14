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
        var metaData = ((AspNetCoreOperationProcessorContext)ctx).ApiDescription.ActionDescriptor.EndpointMetadata;
        var epDef = metaData.OfType<EndpointDefinition>().SingleOrDefault(); //use shortcut `ctx.GetEndpointDefinition()` for your own processors

        if (epDef is null)
            return true; //this is not a fastendpoint

        var apiDescription = ((AspNetCoreOperationProcessorContext)ctx).ApiDescription;

        //fix missing path parameters
        var opPath = ctx.OperationDescription.Path = $"/{StripRouteConstraints(apiDescription.RelativePath!.TrimStart('~').TrimEnd('/'))}";

        var epVer = epDef.Version.Current;
        var startingRelVer = epDef.Version.StartingReleaseVersion;
        var version = $"/{GlobalConfig.VersioningPrefix ?? "v"}{epVer}";
        var routePrefix = "/" + (GlobalConfig.EndpointRoutePrefix ?? "_");
        var bareRoute = opPath.Remove(routePrefix).Remove(version);
        var nameMetaData = metaData.OfType<EndpointNameMetadata>().LastOrDefault();
        var op = ctx.OperationDescription.Operation;
        var reqContent = op.RequestBody?.Content;
        var serializerSettings = ((NewtonsoftJsonSchemaGeneratorSettings)ctx.SchemaGenerator.Settings).SerializerSettings;
        var serializer = JsonSerializer.Create(serializerSettings);

        //set operation id if user has specified
        if (nameMetaData is not null)
            op.OperationId = nameMetaData.EndpointName;

        //set operation tag
        if (docOpts.AutoTagPathSegmentIndex > 0 && !epDef.DontAutoTagEndpoints)
        {
            var overrideVal = metaData.OfType<AutoTagOverride>().SingleOrDefault()?.TagName;
            string? tag = null;

            if (overrideVal is not null)
                tag = TagName(overrideVal, docOpts.TagCase, docOpts.TagStripSymbols);
            else
            {
                var segments = bareRoute.Split('/').Where(s => s != string.Empty).ToArray();
                if (segments.Length >= docOpts.AutoTagPathSegmentIndex)
                    tag = TagName(segments[docOpts.AutoTagPathSegmentIndex - 1], docOpts.TagCase, docOpts.TagStripSymbols);
            }
            if (tag is not null)
                op.Tags.Add(tag);
        }

        //this metadata is consumed and later removed by the document processor.
        var extData = op.ExtensionData ??= new Dictionary<string, object?>();
        extData[FERouteKey] = $"{ctx.OperationDescription.Method}:{bareRoute}";
        extData[FEVersionKey] = epVer;
        extData[FEStartingReleaseKey] = startingRelVer;
        extData[FEDeprecatedAtKey] = epDef.Version.DeprecatedAt;

        //fix request content-types not displaying correctly
        if (reqContent?.Count > 0)
        {
            var contentVal = reqContent.FirstOrDefault().Value;
            var list = new List<KeyValuePair<string, OpenApiMediaType>>(op.Consumes.Count);
            for (var i = 0; i < op.Consumes.Count; i++)
                list.Add(new(op.Consumes[i], contentVal));
            reqContent.Clear();
            foreach (var c in list)
                reqContent.Add(c);
        }

        ApplyResponseMetadata(ctx, metaData, epDef, op, serializer);

        //set endpoint summary & description
        op.Summary = epDef.EndpointSummary?.Summary ?? epDef.EndpointType.GetSummary();
        op.Description = epDef.EndpointSummary?.Description ?? epDef.EndpointType.GetDescription();

        //set endpoint deprecated status when marked with [Obsolete] attribute
        var isObsolete = epDef.EndpointType.GetCustomAttribute<ObsoleteAttribute>() is not null;
        if (isObsolete)
            op.IsDeprecated = true;

        ApplyResponseDescriptions(epDef, apiDescription, op);

        if (GlobalConfig.IsUsingAspVersioning)
        {
            //because asp-versioning adds the version route segment as a path parameter
            for (var i = apiDescription.ParameterDescriptions.Count - 1; i >= 0; i--)
            {
                if (apiDescription.ParameterDescriptions[i].Source != Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Body)
                    apiDescription.ParameterDescriptions.RemoveAt(i);
            }
        }

        var reqDtoType = apiDescription.ParameterDescriptions.FirstOrDefault()?.Type;
        var reqDtoIsList = reqDtoType?.GetInterfaces().Contains(Types.IEnumerable);
        var isGetRequest = apiDescription.HttpMethod == "GET";
        var reqDtoProps = reqDtoIsList is true
                              ? null
                              : reqDtoType?.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy).ToList();

        if (reqDtoType != Types.EmptyRequest && reqDtoProps?.Count == 0 && !GlobalConfig.AllowEmptyRequestDtos) //see: RequestBinder.cs > static ctor
        {
            throw new NotSupportedException(
                "Request DTOs without any publicly accessible properties are not supported. " +
                $"Offending Endpoint: [{epDef.EndpointType.FullName}] " +
                $"Offending DTO type: [{reqDtoType!.FullName}]");
        }

        //store unique request param description + example (from each consumes/content type) for later use.
        //todo: this is not ideal in case two consumes dtos has a prop with the same name.
        var reqParamDescriptions = BuildRequestParamDescriptions(reqContent, epDef, serializer);
        ApplyRequestParamDescriptions(reqContent, reqParamDescriptions);

        var propsToRemoveFromExample = new List<string>();

        //remove dto props that are either marked with [JsonIgnore]/[HideFromDocs] or not publicly settable
        if (reqDtoProps != null)
        {
            foreach (var p in reqDtoProps.Where(
                                             p => p.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition == JsonIgnoreCondition.Always ||
                                                  p.IsDefined(Types.HideFromDocsAttribute) ||
                                                  p.GetSetMethod()?.IsPublic is not true)
                                         .ToArray())
            {
                RemovePropFromRequestBodyContent(p.Name, reqContent, propsToRemoveFromExample, docOpts);
                reqDtoProps.Remove(p);
            }
        }

        var paramCtx = new ParamCreationContext(ctx, docOpts, serializer, reqParamDescriptions, apiDescription.RelativePath!);

        //add a path param for each route param such as /{xxx}/{yyy}/{zzz}
        var reqParams = RouteParamsRegex()
                        .Matches(opPath)
                        .Select(
                            m =>
                            {
                                var pInfo = reqDtoProps?.SingleOrDefault(
                                    p =>
                                    {
                                        var pName = p.GetCustomAttribute<BindFromAttribute>()?.Name ?? p.Name;

                                        if (!string.Equals(pName, m.Value, StringComparison.OrdinalIgnoreCase))
                                            return false;

                                        //need to match complete segments including parenthesis:
                                        //https://github.com/FastEndpoints/FastEndpoints/issues/709
                                        ctx.OperationDescription.Path = opPath = opPath.Replace(
                                                                            $"{{{m.Value}}}",
                                                                            $"{{{m.Value.ApplyPropNamingPolicy(docOpts)}}}");

                                        RemovePropFromRequestBodyContent(p.Name, reqContent, propsToRemoveFromExample, docOpts);

                                        return true;
                                    });

                                return CreateParam(paramCtx, OpenApiParameterKind.Path, pInfo, m.Value, true);
                            })
                        .ToList();

        //add query params for properties marked with [QueryParam] or for all props if it's a GET request
        if (reqDtoType is not null)
        {
            var qParams = reqDtoProps?
                          .Where(
                              p => ShouldAddQueryParam(
                                  p,
                                  reqParams,
                                  isGetRequest && !docOpts.EnableGetRequestsWithBody,
                                  docOpts)) //user wants body in GET requests
                          .Select(
                              p =>
                              {
                                  RemovePropFromRequestBodyContent(p.Name, reqContent, propsToRemoveFromExample, docOpts);

                                  return CreateParam(paramCtx, OpenApiParameterKind.Query, p);
                              })
                          .ToList();

            if (qParams?.Count > 0)
                reqParams.AddRange(qParams);
        }

        //add request params depending on [From*] attribute annotations on dto props
        if (reqDtoProps is not null)
        {
            foreach (var p in reqDtoProps)
            {
                foreach (var attribute in p.GetCustomAttributes())
                {
                    switch (attribute)
                    {
                        case FromHeaderAttribute hAttrib: //add header params if there are any props marked with [FromHeader] attribute
                        {
                            var pName = hAttrib.HeaderName ?? p.Name;

                            if (_illegalHeaderNames.Any(n => n.Equals(pName, StringComparison.OrdinalIgnoreCase)))
                            {
                                RemovePropFromRequestBodyContent(p.Name, reqContent, propsToRemoveFromExample, docOpts);

                                continue;
                            }

                            reqParams.Add(CreateParam(paramCtx, OpenApiParameterKind.Header, p, pName, hAttrib.IsRequired));

                            //remove corresponding json body field if it's required. allow binding only from header.
                            if (hAttrib.IsRequired || hAttrib.RemoveFromSchema)
                                RemovePropFromRequestBodyContent(p.Name, reqContent, propsToRemoveFromExample, docOpts);

                            break;
                        }

                        case FromCookieAttribute cAttrib: //add header params if there are any props marked with [FromHeader] attribute
                        {
                            var pName = cAttrib.CookieName ?? p.Name;

                            reqParams.Add(CreateParam(paramCtx, OpenApiParameterKind.Cookie, p, pName, cAttrib.IsRequired));

                            //remove corresponding json body field if it's required. allow binding only from cookie.
                            if (cAttrib.IsRequired || cAttrib.RemoveFromSchema)
                                RemovePropFromRequestBodyContent(p.Name, reqContent, propsToRemoveFromExample, docOpts);

                            break;
                        }

                        //can only be bound from claim since it's required. so remove prop from body.
                        //can only be bound from permission since it's required. so remove prop from body.
                        case FromClaimAttribute cAttrib when cAttrib.IsRequired || cAttrib.RemoveFromSchema:
                        case HasPermissionAttribute pAttrib when pAttrib.IsRequired || pAttrib.RemoveFromSchema:
                            RemovePropFromRequestBodyContent(p.Name, reqContent, propsToRemoveFromExample, docOpts);

                            break;
                    }
                }
            }
        }

        //fix IFormFile props in OAS2 - remove from request body and add as a request param
        if (ctx.IsSwagger2() && reqDtoProps is not null)
        {
            foreach (var p in reqDtoProps.ToArray())
            {
                if (p.PropertyType != Types.IFormFile)
                    continue;

                RemovePropFromRequestBodyContent(p.Name, reqContent, propsToRemoveFromExample, docOpts);
                reqDtoProps.Remove(p);
                reqParams.Add(CreateParam(paramCtx, OpenApiParameterKind.FormData, p));
            }
        }

        //add idempotency header param if applicable
        if (epDef.IdempotencyOptions is not null)
        {
            var prm = CreateParam(paramCtx, OpenApiParameterKind.Header, null, epDef.IdempotencyOptions.HeaderName, true);
            prm.Example = epDef.IdempotencyOptions.SwaggerExampleGenerator?.Invoke();
            prm.Description = epDef.IdempotencyOptions.SwaggerHeaderDescription;
            if (epDef.IdempotencyOptions.SwaggerHeaderType is not null)
                prm.Schema = JsonSchema.FromType(epDef.IdempotencyOptions.SwaggerHeaderType);
            reqParams.Add(prm);
        }

        if (epDef.X402PaymentMetadata is not null)
        {
            var prm = CreateParam(paramCtx, OpenApiParameterKind.Header, null, X402Constants.PaymentSignatureHeader, false);
            prm.Name = X402Constants.PaymentSignatureHeader;
            prm.Description = "Base64-encoded x402 payment payload.";
            prm.Schema = _x402HeaderSchema;
            reqParams.Add(prm);
        }

        foreach (var p in reqParams)
        {
            if (GlobalConfig.IsUsingAspVersioning)
            {
                //remove any duplicate params - ref: https://github.com/FastEndpoints/FastEndpoints/issues/560
                for (var i = op.Parameters.Count - 1; i >= 0; i--)
                {
                    var prm = op.Parameters[i];
                    if (prm.Name == p.Name && prm.Kind == p.Kind)
                        op.Parameters.RemoveAt(i);
                }
            }

            op.Parameters.Add(p);
        }

        //remove request body if this is a GET request (swagger ui/fetch client doesn't support GET with body).
        //note: user can decide to allow GET requests with body via EnableGetRequestsWithBody setting.
        //or if there are no properties left on the request dto after above operations.
        //only if the request dto is not a list.
        if ((isGetRequest && !docOpts.EnableGetRequestsWithBody) || reqContent?.HasNoProperties() is true)
        {
            if (reqDtoIsList is false)
            {
                op.RequestBody = null;

                for (var i = op.Parameters.Count - 1; i >= 0; i--)
                {
                    if (op.Parameters[i].Kind == OpenApiParameterKind.Body)
                        op.Parameters.RemoveAt(i);
                }
            }
        }

        if (docOpts.RemoveEmptyRequestSchema)
            RemoveEmptyRequestSchemas(ctx.Document.Components.Schemas);

        var fromBodyProp = reqDtoProps?.Where(p => p.IsDefined(Types.FromBodyAttribute, false)).FirstOrDefault();
        var fromFormProp = reqDtoProps?.Where(p => p.IsDefined(Types.FromFormAttribute, false)).FirstOrDefault();
        ReplaceRequestBodyFromProperty(op, ctx.Document.Components.Schemas, paramCtx, fromBodyProp, true);
        ReplaceRequestBodyFromProperty(op, ctx.Document.Components.Schemas, paramCtx, fromFormProp, false);
        ApplyRequestExamples(epDef, op, reqContent, serializer, propsToRemoveFromExample, fromBodyProp, fromFormProp);

        return true;
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

    static void RemovePropFromRequestBodyContent(string propName,
                                                 IDictionary<string, OpenApiMediaType>? content,
                                                 List<string> propsToRemoveFromExample,
                                                 DocumentOptions docOpts)
    {
        if (content is null)
            return;

        propName = propName.ApplyPropNamingPolicy(docOpts);

        propsToRemoveFromExample.Add(propName);

        foreach (var c in content)
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

    void ApplyResponseMetadata(OperationProcessorContext ctx, IEnumerable<object> metaData, EndpointDefinition epDef, OpenApiOperation op, JsonSerializer serializer)
    {
        if (op.Responses.Count == 0)
            return;

        var responseMetas = BuildResponseMetadata(ctx, metaData, epDef, serializer);

        if (responseMetas.Count == 0)
            return;

        ApplyIResultResponseWorkaround(ctx, op, responseMetas);

        foreach (var rsp in op.Responses)
        {
            var mediaType = rsp.Value.Content.FirstOrDefault().Value;

            if (responseMetas.TryGetValue(rsp.Key, out var meta))
            {
                if (mediaType is not null && meta.Example is not null)
                    mediaType.Example = meta.Example;

                AddResponseHeaders(ctx, serializer, rsp.Value, meta);

                if (mediaType is not null)
                    ReplaceResponseContentTypes(rsp.Value, mediaType, meta.ContentTypes);
            }

            if (docOpts.UseOneOfForPolymorphism)
                FixResponsePolymorphism(rsp.Value);

            FixBinaryResponseSchemas(rsp.Value);
            AddX402ResponseHeaders(epDef, rsp.Key, rsp.Value);
        }
    }

    static Dictionary<string, ResponseMeta> BuildResponseMetadata(OperationProcessorContext ctx, IEnumerable<object> metaData, EndpointDefinition epDef, JsonSerializer serializer)
    {
        return metaData
               .OfType<IProducesResponseTypeMetadata>()
               .GroupBy(
                   m => m.StatusCode,
                   (k, g) =>
                   {
                       var meta = g.Last();
                       object? example = null;
                       _ = epDef.EndpointSummary?.ResponseExamples.TryGetValue(k, out example);
                       example = meta.GetExampleFromMetaData() ?? example;
                       example = example is not null ? JToken.FromObject(example, serializer) : null;

                       if (ctx.IsSwagger2() && example is JToken { Type: JTokenType.Array } token)
                           example = token.ToString();

                       return new ResponseMeta(
                           k.ToString(),
                           [..meta.ContentTypes],
                           example,
                           epDef.EndpointSummary?.ResponseHeaders.Where(h => h.StatusCode == k).ToArray(),
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

    void AddResponseHeaders(OperationProcessorContext ctx, JsonSerializer serializer, OpenApiResponse response, ResponseMeta meta)
    {
        if (meta.DtoType is not null)
        {
            foreach (var p in meta.DtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                                  .Where(p => p.IsDefined(Types.ToHeaderAttribute)))
            {
                var headerName = p.GetCustomAttribute<ToHeaderAttribute>()?.HeaderName ?? p.Name.ApplyPropNamingPolicy(docOpts);
                var summaryTag = p.GetXmlDocsSummary();
                var schema = ctx.SchemaGenerator.Generate(p.PropertyType);
                response.Headers[headerName] = new()
                {
                    Description = summaryTag,
                    Example = p.GetExampleJToken(serializer) ?? schema.ToSampleJson(),
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
                    Example = hdr.Example is not null ? JToken.FromObject(hdr.Example, serializer) : null,
                    Schema = hdr.Example is not null ? ctx.SchemaGenerator.Generate(hdr.Example.GetType()) : null
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

    static void ApplyResponseDescriptions(EndpointDefinition epDef, ApiDescription apiDescription, OpenApiOperation op)
    {
        foreach (var oaResp in op.Responses.Where(r => string.IsNullOrWhiteSpace(r.Value.Description)).ToArray())
        {
            if (_defaultDescriptions.TryGetValue(oaResp.Key, out var description))
                oaResp.Value.Description = description;

            var statusCode = Convert.ToInt32(oaResp.Key);

            if (epDef.EndpointSummary?.Responses.ContainsKey(statusCode) is true)
                oaResp.Value.Description = epDef.EndpointSummary.Responses[statusCode];

            if (epDef.EndpointSummary?.ResponseParams.ContainsKey(statusCode) is not true || oaResp.Value.Schema is null)
                continue;

            var propDescriptions = epDef.EndpointSummary.ResponseParams[statusCode];
            var respDtoProps = apiDescription
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

    static Dictionary<string, ParamDescription> BuildRequestParamDescriptions(IDictionary<string, OpenApiMediaType>? reqContent,
                                                                              EndpointDefinition epDef,
                                                                              JsonSerializer serializer)
    {
        var descriptions = new Dictionary<string, ParamDescription>(StringComparer.OrdinalIgnoreCase);

        if (reqContent is not null)
        {
            foreach (var c in reqContent)
            {
                foreach (var prop in c.GetAllProperties())
                {
                    descriptions[prop.Key] = new(
                        prop.Value.Description,
                        prop.Value.Example != null ? JToken.FromObject(prop.Value.Example, serializer) : null);
                }
            }
        }

        if (epDef.EndpointSummary is not null)
        {
            foreach (var param in epDef.EndpointSummary.Params)
                descriptions.GetOrAdd(param.Key, new()).Description = param.Value;
        }

        if (epDef.EndpointSummary?.RequestExamples.Count is > 0)
        {
            var example = epDef.EndpointSummary.RequestExamples.First().Value;

            if (example is not IEnumerable)
            {
                var jToken = JToken.FromObject(example, serializer);

                foreach (var prop in jToken)
                {
                    var p = (JProperty)prop;
                    descriptions.GetOrAdd(p.Name, new()).Example = p.Value;
                }
            }
        }

        return descriptions;
    }

    static void ApplyRequestParamDescriptions(IDictionary<string, OpenApiMediaType>? reqContent,
                                              Dictionary<string, ParamDescription> reqParamDescriptions)
    {
        if (reqContent is null)
            return;

        foreach (var c in reqContent)
        {
            foreach (var prop in c.GetAllRequestProperties())
            {
                if (!reqParamDescriptions.TryGetValue(prop.Key, out var x))
                    continue;

                prop.Value.Description = x.Description;
                prop.Value.Example = x.Example;
            }
        }
    }

    static void ReplaceRequestBodyFromProperty(OpenApiOperation op,
                                               IDictionary<string, JsonSchema> schemas,
                                               ParamCreationContext paramCtx,
                                               PropertyInfo? bodyProp,
                                               bool isJsonPatchAware)
    {
        var body = op.Parameters.FirstOrDefault(x => x.Kind == OpenApiParameterKind.Body);

        if (bodyProp is null || body is null || op.RequestBody is null)
            return;

        var oldBodyName = op.RequestBody.Name;
        var bodyParam = CreateParam(paramCtx, OpenApiParameterKind.Body, bodyProp, bodyProp.Name, true);

        //otherwise xml docs from properties won't be considered due to existence of a schema level example generated from prm.ActualSchema.ToSampleJson()
        bodyParam.Example = null;

        op.RequestBody.Content.FirstOrDefault().Value.Schema = bodyParam.Schema;

        if (isJsonPatchAware && op.RequestBody.Content.TryGetValue("application/json-patch+json", out var patchContent))
            patchContent.Schema = TryGetJsonPatchArraySchema(bodyParam.Schema) ?? TryGetJsonPatchArraySchema(patchContent.Schema) ?? bodyParam.Schema;

        op.RequestBody.IsRequired = bodyParam.IsRequired;
        op.RequestBody.Description = bodyParam.Description;
        op.RequestBody.Name = bodyParam.Name;
        op.RequestBody.Position = null;
        schemas.Remove(oldBodyName);
    }

    static void ApplyRequestExamples(EndpointDefinition epDef,
                                     OpenApiOperation op,
                                     IDictionary<string, OpenApiMediaType>? reqContent,
                                     JsonSerializer serializer,
                                     List<string> propsToRemoveFromExample,
                                     PropertyInfo? fromBodyProp,
                                     PropertyInfo? fromFormProp)
    {
        var summary = epDef.EndpointSummary;

        if (summary?.RequestExamples.Count is not > 0)
            return;

        foreach (var requestBody in op.Parameters.Where(x => x.Kind == OpenApiParameterKind.Body))
        {
            var exCount = summary.RequestExamples.Count;

            if (exCount == 1)
            {
                requestBody.ActualSchema.Example = GetExampleObjectFrom(summary.RequestExamples.First());

                continue;
            }

            foreach (var group in summary.RequestExamples.GroupBy(e => e.Label).Where(g => g.Count() > 1))
            {
                var i = 1;

                foreach (var ex in group)
                {
                    ex.Label += $" {i}";
                    i++;
                }
            }

            foreach (var example in summary.RequestExamples)
            {
                var firstContent = reqContent?.FirstOrDefault().Value;

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
                return JToken.FromObject(input, serializer);

            var example = JObject.FromObject(input, serializer);

            foreach (var p in example.Properties().ToArray())
            {
                if (propsToRemoveFromExample.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                    p.Remove();
            }

            return example;
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
}

sealed class ResponseMeta(string key,
                          IReadOnlyCollection<string> contentTypes,
                          object? example,
                          ResponseHeader[]? userHeaders,
                          Type? dtoType,
                          bool isIResult)
{
    public string Key { get; } = key;
    public IReadOnlyCollection<string> ContentTypes { get; } = contentTypes;
    public object? Example { get; } = example;
    public ResponseHeader[]? UserHeaders { get; } = userHeaders;
    public Type? DtoType { get; } = dtoType;
    public bool IsIResult { get; } = isIResult;
}

sealed class ParamDescription(string? description = null, JToken? example = null)
{
    public string? Description { get; set; } = description;
    public JToken? Example { get; set; } = example;
}