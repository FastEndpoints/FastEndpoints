﻿using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Namotion.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FastEndpoints.Swagger;

sealed class OperationProcessor : IOperationProcessor
{
    static readonly TextInfo _textInfo = CultureInfo.InvariantCulture.TextInfo;
    static readonly Regex _routeParamsRegex = new("(?<={)(?:.*?)*(?=})", RegexOptions.Compiled);
    static readonly Regex _routeConstraintsRegex = new("(?<={)([^?:}]+)[^}]*(?=})", RegexOptions.Compiled);

    static readonly Dictionary<string, string> _defaultDescriptions = new()
    {
        { "200", "Success" },
        { "201", "Created" },
        { "202", "Accepted" },
        { "204", "No Content" },
        { "400", "Bad Request" },
        { "401", "Unauthorized" },
        { "403", "Forbidden" },
        { "404", "Not Found" },
        { "405", "Method Not Allowed" },
        { "406", "Not Acceptable" },
        { "429", "Too Many Requests" },
        { "500", "Server Error" }
    };

    readonly DocumentOptions _opts;

    public OperationProcessor(DocumentOptions documentOptions)
    {
        _opts = documentOptions;
    }

    public bool Process(OperationProcessorContext ctx)
    {
        var metaData = ((AspNetCoreOperationProcessorContext)ctx).ApiDescription.ActionDescriptor.EndpointMetadata;
        var epDef = metaData.OfType<EndpointDefinition>().SingleOrDefault(); //use shortcut `ctx.GetEndpointDefinition()` for your own processors

        if (epDef is null)
            return true; //this is not a fastendpoint

        var apiDescription = ((AspNetCoreOperationProcessorContext)ctx).ApiDescription;
        var opPath = ctx.OperationDescription.Path = $"/{StripRouteConstraints(apiDescription.RelativePath!)}"; //fix missing path parameters
        var apiVer = epDef.Version.Current;
        var version = $"/{GlobalConfig.VersioningPrefix ?? "v"}{apiVer}";
        var routePrefix = "/" + (GlobalConfig.EndpointRoutePrefix ?? "_");
        var bareRoute = opPath.Remove(routePrefix).Remove(version);
        var nameMetaData = metaData.OfType<EndpointNameMetadata>().LastOrDefault();
        var op = ctx.OperationDescription.Operation;
        var reqContent = op.RequestBody?.Content;
        var serializer = JsonSerializer.Create(ctx.SchemaGenerator.Settings.ActualSerializerSettings);

        //set operation id if user has specified
        if (nameMetaData is not null)
            op.OperationId = nameMetaData.EndpointName;

        //set operation tag
        if (_opts.AutoTagPathSegmentIndex > 0 && !epDef.DontAutoTagEndpoints)
        {
            var segments = bareRoute.Split('/').Where(s => s != string.Empty).ToArray();
            if (segments.Length >= _opts.AutoTagPathSegmentIndex)
                op.Tags.Add(TagName(segments[_opts.AutoTagPathSegmentIndex - 1], _opts.TagCase));
        }

        //this will be later removed from document processor. this info is needed by the document processor.
        op.Tags.Add($"|{ctx.OperationDescription.Method}:{bareRoute}|{apiVer}|{epDef.Version.DeprecatedAt}");

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

        //fix response content-types not displaying correctly
        //also set user provided response examples and response headers
        if (op.Responses.Count > 0)
        {
            var metas = metaData
                        .OfType<IProducesResponseTypeMetadata>()
                        .GroupBy(
                            m => m.StatusCode,
                            (k, g) =>
                            {
                                object? example = null;
                                _ = epDef.EndpointSummary?.ResponseExamples.TryGetValue(k, out example);
                                example = g.Last().GetExampleFromMetaData() ?? example;
                                example = example is not null ? JToken.FromObject(example, serializer) : null;

                                if (ctx.Settings.SchemaType == SchemaType.Swagger2 &&
                                    example is JToken token &&
                                    token.Type == JTokenType.Array)
                                    example = token.ToString();

                                return new
                                {
                                    key = k.ToString(),
                                    cTypes = g.Last().ContentTypes,
                                    example,
                                    headers = epDef.EndpointSummary?.ResponseHeaders.Where(h => h.StatusCode == k).ToArray()
                                };
                            })
                        .ToDictionary(x => x.key);

            if (metas.Count > 0)
            {
                foreach (var rsp in op.Responses)
                {
                    var cTypes = metas[rsp.Key].cTypes;
                    var mediaType = rsp.Value.Content.FirstOrDefault().Value;

                    if (metas.TryGetValue(rsp.Key, out var x))
                    {
                        if (x.example is not null)
                            mediaType.Example = x.example;

                        if (x.headers?.Any() is true)
                        {
                            foreach (var hdr in x.headers)
                            {
                                rsp.Value.Headers.Add(
                                    new(
                                        hdr.HeaderName,
                                        new()
                                        {
                                            Description = hdr.Description,
                                            Example = hdr.Example is not null ? JToken.FromObject(hdr.Example, serializer) : null,
                                            Schema = hdr.Example is not null ? ctx.SchemaGenerator.Generate(hdr.Example?.GetType()) : null
                                        }));
                            }
                        }
                    }

                    rsp.Value.Content.Clear();
                    foreach (var ct in cTypes)
                        rsp.Value.Content.Add(new(ct, mediaType));
                }
            }
        }

        //set endpoint summary & description
        op.Summary = epDef.EndpointSummary?.Summary ?? epDef.EndpointType.GetSummary();
        op.Description = epDef.EndpointSummary?.Description ?? epDef.EndpointType.GetDescription();

        //set endpoint deprecated status when marked with [Obsolete] attribute
        var isObsolete = epDef.EndpointType.GetCustomAttribute<ObsoleteAttribute>() is not null;
        if (isObsolete)
            op.IsDeprecated = true;

        //set response descriptions
        op.Responses
          .Where(r => string.IsNullOrWhiteSpace(r.Value.Description))
          .ToList()
          .ForEach(
              res =>
              {
                  if (_defaultDescriptions.TryGetValue(res.Key, out var description))
                      res.Value.Description = description; //first set the default text

                  var key = Convert.ToInt32(res.Key);

                  if (epDef.EndpointSummary?.Responses.ContainsKey(key) is true)
                      res.Value.Description = epDef.EndpointSummary.Responses[key]; //then take values from summary object

                  if (epDef.EndpointSummary?.ResponseParams.ContainsKey(key) is true && res.Value.Schema is not null)
                  {
                      //set response dto property descriptions

                      var responseDescriptions = epDef.EndpointSummary.ResponseParams[key];

                      foreach (var prop in res.GetAllProperties())
                      {
                          if (responseDescriptions.TryGetValue(prop.Key, out var responseDescription))
                              prop.Value.Description = responseDescription;
                      }
                  }
              });

        var reqDtoType = apiDescription.ParameterDescriptions.FirstOrDefault()?.Type;
        var reqDtoIsList = reqDtoType?.GetInterfaces().Contains(Types.IEnumerable);
        var reqDtoProps = reqDtoIsList is true
                              ? null
                              : reqDtoType?.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy).ToList();
        var isGetRequest = apiDescription.HttpMethod == "GET";

        //store unique request param description (from each consumes/content type) for later use.
        //these are the xml comments from dto classes
        //todo: this is not ideal in case two consumes dtos has a prop with the same name.
        var reqParamDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (reqContent is not null)
        {
            foreach (var c in reqContent)
            {
                foreach (var prop in c.GetAllProperties())
                    reqParamDescriptions[prop.Key] = prop.Value.Description;
            }
        }

        //also add descriptions from user supplied summary request params overriding the above
        if (epDef.EndpointSummary is not null)
        {
            foreach (var param in epDef.EndpointSummary.Params)
                reqParamDescriptions[param.Key] = param.Value;
        }

        //override req param descriptions for each consumes/content type from user supplied summary request param descriptions
        if (reqContent is not null)
        {
            foreach (var c in reqContent)
            {
                foreach (var prop in c.GetAllProperties())
                {
                    if (reqParamDescriptions.TryGetValue(prop.Key, out var description))
                        prop.Value.Description = description;
                }
            }
        }

        var propsToRemoveFromExample = new List<string>();

        //remove dto props that are either marked with [JsonIgnore] or not publicly settable
        if (reqDtoProps != null)
        {
            foreach (var p in reqDtoProps.Where(p => p.IsDefined(Types.JsonIgnoreAttribute) || p.GetSetMethod()?.IsPublic is not true)
                                         .ToArray()) //prop has no public setter or has ignore attribute
            {
                RemovePropFromRequestBodyContent(p.Name, reqContent, propsToRemoveFromExample);
                reqDtoProps.Remove(p);
            }
        }

        //add a path param for each route param such as /{xxx}/{yyy}/{zzz}
        var reqParams = _routeParamsRegex
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

                                        RemovePropFromRequestBodyContent(p.Name, reqContent, propsToRemoveFromExample);

                                        return true;
                                    });

                                return CreateParam(
                                    ctx: ctx,
                                    prop: pInfo,
                                    paramName: m.Value,
                                    isRequired: true,
                                    kind: OpenApiParameterKind.Path,
                                    descriptions: reqParamDescriptions,
                                    docOpts: _opts);
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
                                  isGetRequest && !_opts.EnableGetRequestsWithBody)) //user wants body in GET requests
                          .Select(
                              p =>
                              {
                                  RemovePropFromRequestBodyContent(p.Name, reqContent, propsToRemoveFromExample);

                                  return CreateParam(
                                      ctx: ctx,
                                      prop: p,
                                      paramName: null,
                                      isRequired: null,
                                      kind: OpenApiParameterKind.Query,
                                      descriptions: reqParamDescriptions,
                                      docOpts: _opts);
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
                    //add header params if there are any props marked with [FromHeader] attribute
                    if (attribute is FromHeaderAttribute hAttrib)
                    {
                        var pName = hAttrib.HeaderName ?? p.Name;
                        reqParams.Add(
                            CreateParam(
                                ctx: ctx,
                                prop: p,
                                paramName: pName,
                                isRequired: hAttrib.IsRequired,
                                kind: OpenApiParameterKind.Header,
                                descriptions: reqParamDescriptions,
                                docOpts: _opts));

                        //remove corresponding json body field if it's required. allow binding only from header.
                        if (hAttrib.IsRequired || hAttrib.RemoveFromSchema)
                            RemovePropFromRequestBodyContent(p.Name, reqContent, propsToRemoveFromExample);
                    }

                    //can only be bound from claim since it's required. so remove prop from body.
                    if (attribute is FromClaimAttribute cAttrib && (cAttrib.IsRequired || cAttrib.RemoveFromSchema))
                        RemovePropFromRequestBodyContent(p.Name, reqContent, propsToRemoveFromExample);

                    //can only be bound from permission since it's required. so remove prop from body.
                    if (attribute is HasPermissionAttribute pAttrib && (pAttrib.IsRequired || pAttrib.RemoveFromSchema))
                        RemovePropFromRequestBodyContent(p.Name, reqContent, propsToRemoveFromExample);
                }
            }
        }

        //fix IFormFile props in OAS2 - remove from request body and add as a request param
        if (ctx.Settings.SchemaType == SchemaType.Swagger2 && reqDtoProps is not null)
        {
            foreach (var p in reqDtoProps.ToArray())
            {
                if (p.PropertyType == Types.IFormFile)
                {
                    RemovePropFromRequestBodyContent(p.Name, reqContent, propsToRemoveFromExample);
                    reqDtoProps.Remove(p);
                    reqParams.Add(
                        CreateParam(
                            ctx: ctx,
                            prop: p,
                            paramName: null,
                            isRequired: null,
                            kind: OpenApiParameterKind.FormData,
                            descriptions: reqParamDescriptions,
                            docOpts: _opts));
                }
            }
        }

        foreach (var p in reqParams)
            op.Parameters.Add(p);

        //remove request body if this is a GET request (swagger ui/fetch client doesn't support GET with body).
        //note: user can decide to allow GET requests with body via EnableGetRequestsWithBody setting.
        //or if there are no properties left on the request dto after above operations.
        //only if the request dto is not a list.
        if ((isGetRequest && !_opts.EnableGetRequestsWithBody) || reqContent?.HasNoProperties() is true)
        {
            if (reqDtoIsList is false)
            {
                op.RequestBody = null;
                foreach (var body in op.Parameters.Where(x => x.Kind == OpenApiParameterKind.Body).ToArray())
                    op.Parameters.Remove(body);
            }
        }

        if (_opts.RemoveEmptyRequestSchema)
        {
            //remove all empty schemas that has no props left
            //these schemas have been flattened so no need to worry about inheritance
            foreach (var s in ctx.Document.Components.Schemas)
            {
                if (s.Value.ActualProperties.Count == 0 && s.Value.IsObject)
                    ctx.Document.Components.Schemas.Remove(s.Key);
            }
        }

        //replace body parameter if a dto property is marked with [FromBody]
        var fromBodyProp = reqDtoProps?.Where(p => p.IsDefined(typeof(FromBodyAttribute), false)).FirstOrDefault();

        if (fromBodyProp is not null)
        {
            foreach (var body in op.Parameters.Where(x => x.Kind == OpenApiParameterKind.Body).ToArray())
            {
                op.Parameters.Remove(body);
                op.Parameters.Add(
                    CreateParam(
                        ctx: ctx,
                        prop: fromBodyProp,
                        paramName: fromBodyProp.Name,
                        isRequired: true,
                        kind: OpenApiParameterKind.Body,
                        descriptions: reqParamDescriptions,
                        docOpts: _opts));
            }
        }

        //set request examples if provided by user
        if (epDef.EndpointSummary?.RequestExamples.Count > 0)
        {
            foreach (var requestBody in op.Parameters.Where(x => x.Kind == OpenApiParameterKind.Body))
            {
                var exCount = epDef.EndpointSummary!.RequestExamples.Count;

                if (exCount == 1)
                    requestBody.ActualSchema.Example = GetExampleFrom(epDef.EndpointSummary?.RequestExamples[0]);
                else
                {
                    for (var i = 0; i < epDef.EndpointSummary.RequestExamples.Count; i++)
                    {
                        reqContent?.First().Value.Examples.Add(
                            key: $"Example {i + 1}",
                            value: new() { Value = GetExampleFrom(epDef.EndpointSummary.RequestExamples[i]) });
                    }
                }
            }

            object? GetExampleFrom(object? requestExample)
            {
                if (requestExample is null)
                    return null;

                object example;

                if (requestExample.GetType().IsAssignableTo(typeof(IEnumerable)))
                    example = JToken.FromObject(requestExample, serializer);
                else
                {
                    example = JObject.FromObject(requestExample, serializer);

                    foreach (var p in ((JObject)example).Properties().ToArray())
                    {
                        if (propsToRemoveFromExample.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                            p.Remove();
                    }
                }

                return example;
            }
        }

        return true;
    }

    static bool ShouldAddQueryParam(PropertyInfo prop, List<OpenApiParameter> reqParams, bool isGetRequest)
    {
        var paramName = prop.Name;

        foreach (var attribute in prop.GetCustomAttributes())
        {
            if (attribute is BindFromAttribute bAtt)
                paramName = bAtt.Name;

            if (attribute is FromHeaderAttribute)
                return false; // because header request params are being added

            if (attribute is FromClaimAttribute cAttrib)
                return !cAttrib.IsRequired; // add param if it's not required. if required only can bind from actual claim.

            if (attribute is HasPermissionAttribute pAttrib)
                return !pAttrib.IsRequired; // add param if it's not required. if required only can bind from actual permission.
        }

        return

            //it's a GET request and request params already has it. so don't add
            (isGetRequest && !reqParams.Any(rp => rp.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase))) ||

            //this prop is marked with [QueryParam], so add. applies to all verbs.
            prop.IsDefined(Types.QueryParamAttribute);
    }

    static void RemovePropFromRequestBodyContent(string propName,
                                                 IDictionary<string, OpenApiMediaType>? content,
                                                 List<string> propsToRemoveFromExample)
    {
        if (content is null)
            return;

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
            schema.RequiredProperties
                  .Remove(key); //because validation schema processor may have added this prop/key, which should be removed when the prop is being removed from the schema
            foreach (var s in schema.AllOf.Union(schema.AllInheritedSchemas))
                Remove(s, key);
        }
    }

    static string StripRouteConstraints(string relativePath)
    {
        var parts = relativePath.Split('/');

        for (var i = 0; i < parts.Length; i++)
            parts[i] = _routeConstraintsRegex.Replace(parts[i], "$1");

        return string.Join("/", parts);
    }

    static string TagName(string input, TagCase tagCase)
    {
        return tagCase switch
        {
            TagCase.None => input,
            TagCase.TitleCase => _textInfo.ToTitleCase(input),
            TagCase.LowerCase => _textInfo.ToLower(input),
            _ => input
        };
    }

    static OpenApiParameter CreateParam(OperationProcessorContext ctx,
                                        PropertyInfo? prop,
                                        string? paramName,
                                        bool? isRequired,
                                        OpenApiParameterKind kind,
                                        Dictionary<string, string>? descriptions,
                                        DocumentOptions docOpts)
    {
        paramName = paramName?.ApplyPropNamingPolicy(docOpts) ??
                    prop?.GetCustomAttribute<BindFromAttribute>()?.Name ?? //don't apply naming policy to attribute value
                    prop?.Name.ApplyPropNamingPolicy(docOpts) ?? throw new InvalidOperationException("param name is required!");

        var prm = ctx.DocumentGenerator.CreatePrimitiveParameter(
            paramName,
            descriptions?.GetValueOrDefault(prop?.Name ?? paramName),
            (prop?.PropertyType ?? Types.String).ToContextualType());

        prm.Kind = kind;
        prm.IsRequired = isRequired ?? !(prop?.IsNullable() ?? true);

        if (ctx.Settings.SchemaType == SchemaType.Swagger2)
            prm.Default = prop?.GetCustomAttribute<DefaultValueAttribute>()?.Value;
        else
            prm.Schema.Default = prop?.GetCustomAttribute<DefaultValueAttribute>()?.Value;

        if (ctx.Settings.GenerateExamples)
        {
            prm.Example = prop?.GetXmlExample();

            if (prm.Example is null && prm.Default is null && prm.Schema.Default is null && prm.IsRequired)
            {
                var jToken = prm.ActualSchema.ToSampleJson();
                prm.Example = jToken.HasValues ? jToken : null;
            }
        }
        prm.IsNullableRaw = null; //if this is not null, nswag generates an incorrect swagger spec for some unknown reason.

        return prm;
    }
}