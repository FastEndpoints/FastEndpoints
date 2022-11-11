using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
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

internal class OperationProcessor : IOperationProcessor
{
    private static readonly TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;
    private static readonly Regex regex = new(@"(?<=\{)[^}]*(?=\})", RegexOptions.Compiled);
    private static readonly Dictionary<string, string> defaultDescriptions = new()
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
        { "500", "Server Error" },
    };

    private readonly int tagIndex;
    private readonly bool removeEmptySchemas;
    private readonly TagCase tagCase;

    public OperationProcessor(int tagIndex, bool removeEmptySchemas, TagCase tagCase)
    {
        this.tagIndex = tagIndex;
        this.removeEmptySchemas = removeEmptySchemas;
        this.tagCase = tagCase;
    }

    public bool Process(OperationProcessorContext ctx)
    {
        var metaData = ((AspNetCoreOperationProcessorContext)ctx).ApiDescription.ActionDescriptor.EndpointMetadata;
        var epDef = metaData.OfType<EndpointDefinition>().SingleOrDefault(); //use shortcut `ctx.GetEndpointDefinition()` for your own processors
        var schemaGeneratorSettings = ctx.SchemaGenerator.Settings;

        if (epDef is null)
            return true; //this is not a fastendpoint

        var apiDescription = ((AspNetCoreOperationProcessorContext)ctx).ApiDescription;
        var opPath = ctx.OperationDescription.Path = $"/{StripRouteConstraints(apiDescription.RelativePath!)}";//fix missing path parameters
        var apiVer = epDef.Version.Current;
        var version = $"/{GlobalConfig.VersioningPrefix ?? "v"}{apiVer}";
        var routePrefix = "/" + (GlobalConfig.EndpointRoutePrefix ?? "_");
        var bareRoute = opPath.Remove(routePrefix).Remove(version);
        var nameMetaData = metaData.OfType<EndpointNameMetadata>().LastOrDefault();
        var op = ctx.OperationDescription.Operation;
        var serializer = Newtonsoft.Json.JsonSerializer.Create(ctx.SchemaGenerator.Settings.ActualSerializerSettings);

        //set operation id if user has specified
        if (nameMetaData is not null)
            op.OperationId = nameMetaData.EndpointName;

        //set operation tag
        if (tagIndex > 0 && !epDef.DontAutoTagEndpoints)
        {
            var segments = bareRoute.Split('/').Where(s => s != string.Empty).ToArray();
            if (segments.Length >= tagIndex)
                op.Tags.Add(TagName(segments[tagIndex - 1], tagCase));
        }

        //this will be later removed from document processor. this info is needed by the document processor.
        op.Tags.Add($"|{ctx.OperationDescription.Method}:{bareRoute}|{apiVer}|{epDef.Version.DeprecatedAt}");

        //fix request content-types not displaying correctly
        var reqContent = op.RequestBody?.Content;
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
        //also set user provided response examples
        if (op.Responses.Count > 0)
        {
            var metas = metaData
             .OfType<IProducesResponseTypeMetadata>()
             .GroupBy(m => m.StatusCode, (k, g) =>
             {
                 object? example = null;
                 _ = epDef.EndpointSummary?.ResponseExamples.TryGetValue(k, out example);
                 example = g.Last().GetExampleFromMetaData() ?? example;
                 example = example is not null ? JToken.FromObject(example, serializer) : null;
                 return new {
                     key = k.ToString(),
                     cTypes = g.Last().ContentTypes,
                     example = example
                 };
             })
             .ToDictionary(x => x.key);

            if (metas.Count > 0)
            {
                foreach (var rsp in op.Responses)
                {
                    var cTypes = metas[rsp.Key].cTypes;
                    var mediaType = rsp.Value.Content.FirstOrDefault().Value;
                    if (metas.TryGetValue(rsp.Key, out var x) && x.example is not null)
                    {
                        mediaType.Example = x.example;
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

        //set response descriptions
        op.Responses
          .Where(r => string.IsNullOrWhiteSpace(r.Value.Description))
          .ToList()
          .ForEach(res =>
          {
              if (defaultDescriptions.ContainsKey(res.Key))
                  res.Value.Description = defaultDescriptions[res.Key]; //first set the default text

              var key = Convert.ToInt32(res.Key);

              if (epDef.EndpointSummary?.Responses.ContainsKey(key) is true)
                  res.Value.Description = epDef.EndpointSummary.Responses[key]; //then take values from summary object

              if (epDef.EndpointSummary?.ResponseParams.ContainsKey(key) is true && res.Value.Schema is not null)
              {
                  //set response dto property descriptions

                  var responseSchema = res.Value.Schema.ActualSchema;
                  var responseDescriptions = epDef.EndpointSummary.ResponseParams[key];
                  foreach (var prop in responseSchema.ActualProperties)
                  {
                      if (responseDescriptions.ContainsKey(prop.Key))
                          prop.Value.Description = responseDescriptions[prop.Key];
                  }

                  if (responseSchema.InheritedSchema is not null)
                  {
                      foreach (var prop in responseSchema.InheritedSchema.ActualProperties)
                      {
                          if (responseDescriptions.ContainsKey(prop.Key))
                              prop.Value.Description = responseDescriptions[prop.Key];
                      }
                  }
              }
          });

        var reqDtoType = apiDescription.ParameterDescriptions.FirstOrDefault()?.Type;
        var reqDtoIsList = reqDtoType?.GetInterfaces().Contains(Types.IEnumerable);
        var reqDtoProps = reqDtoIsList is true ? null : reqDtoType?.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy).ToList();
        var isGETRequest = apiDescription.HttpMethod == "GET";

        //store unique request param description (from each consumes/content type) for later use.
        //these are the xml comments from dto classes
        //todo: this is not ideal in case two consumes dtos has a prop with the same name.
        var reqParamDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (op.RequestBody is not null)
        {
            foreach (var c in op.RequestBody.Content)
            {
                foreach (var prop in c.Value.Schema.ActualSchema.ActualProperties)
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
        if (op.RequestBody is not null)
        {
            foreach (var c in op.RequestBody.Content)
            {
                foreach (var prop in c.Value.Schema.ActualSchema.ActualProperties)
                {
                    if (reqParamDescriptions.ContainsKey(prop.Key))
                        prop.Value.Description = reqParamDescriptions[prop.Key];
                }

                if (c.Value.Schema.ActualSchema.InheritedSchema is not null)
                {
                    foreach (var prop in c.Value.Schema.ActualSchema.InheritedSchema.ActualProperties)
                    {
                        if (reqParamDescriptions.ContainsKey(prop.Key))
                            prop.Value.Description = reqParamDescriptions[prop.Key];
                    }
                }
            }
        }

        var reqParams = new List<OpenApiParameter>();
        var propsToRemoveFromExample = new List<string>();

        if (reqDtoProps != null)
        {
            foreach (var p in reqDtoProps.Where(p => p.GetSetMethod()?.IsPublic is not true).ToArray()) //prop has no public setter
            {
                RemovePropFromRequestBodyContent(p.Name, op.RequestBody?.Content, propsToRemoveFromExample);
                reqDtoProps.Remove(p);
            }
        }

        //add a path param for each route param such as /{xxx}/{yyy}/{zzz}
        reqParams = regex
            .Matches(apiDescription?.RelativePath!)
            .Select(m =>
            {
                var pInfo = reqDtoProps?.SingleOrDefault(p =>
                {
                    var pName = p.GetCustomAttribute<BindFromAttribute>()?.Name ?? p.Name;
                    if (string.Equals(pName, ActualParamName(m.Value), StringComparison.OrdinalIgnoreCase))
                    {
                        RemovePropFromRequestBodyContent(p.Name, op.RequestBody?.Content, propsToRemoveFromExample);
                        return true;
                    }
                    return false;
                });

                return new OpenApiParameter
                {
                    Name = ActualParamName(m.Value),
                    Kind = OpenApiParameterKind.Path,
                    IsRequired = true,
                    Schema = ctx.SchemaGenerator.Generate(pInfo?.PropertyType ?? Types.String, ctx.SchemaResolver),
                    Description = reqParamDescriptions.GetValueOrDefault(ActualParamName(m.Value)),
                    Default = pInfo?.GetCustomAttribute<DefaultValueAttribute>()?.Value,
                    Example = pInfo?.GetExample()
                };
            })
            .ToList();

        //add query params for properties marked with [QueryParam] or for all props if it's a GET request
        if (reqDtoType is not null)
        {
            var qParams = reqDtoProps?
                .Where(p => ShouldAddQueryParam(p, reqParams, isGETRequest))
                .Select(p =>
                {
                    RemovePropFromRequestBodyContent(p.Name, op.RequestBody?.Content, propsToRemoveFromExample);
                    return new OpenApiParameter
                    {
                        Name = p.GetCustomAttribute<BindFromAttribute>()?.Name ?? p.Name,
                        IsRequired = !p.IsNullable(),
                        Schema = ctx.SchemaGenerator.Generate(p.PropertyType, ctx.SchemaResolver),
                        Kind = OpenApiParameterKind.Query,
                        Description = reqParamDescriptions.GetValueOrDefault(p.Name),
                        Default = p.GetCustomAttribute<DefaultValueAttribute>()?.Value,
                        Example = p.GetExample()
                    };
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
                        reqParams.Add(new OpenApiParameter
                        {
                            Name = pName,
                            IsRequired = hAttrib.IsRequired,
                            Schema = ctx.SchemaGenerator.Generate(p.PropertyType, ctx.SchemaResolver),
                            Kind = OpenApiParameterKind.Header,
                            Description = reqParamDescriptions.GetValueOrDefault(pName),
                            Default = p.GetCustomAttribute<DefaultValueAttribute>()?.Value,
                            Example = p.GetExample()
                        });

                        //remove corresponding json body field if it's required. allow binding only from header.
                        if (hAttrib.IsRequired)
                            RemovePropFromRequestBodyContent(p.Name, op.RequestBody?.Content, propsToRemoveFromExample);
                    }

                    //can only be bound from claim since it's required. so remove prop from body.
                    if (attribute is FromClaimAttribute cAttrib && cAttrib.IsRequired)
                        RemovePropFromRequestBodyContent(p.Name, op.RequestBody?.Content, propsToRemoveFromExample);

                    //can only be bound from permission since it's required. so remove prop from body.
                    if (attribute is HasPermissionAttribute pAttrib && pAttrib.IsRequired)
                        RemovePropFromRequestBodyContent(p.Name, op.RequestBody?.Content, propsToRemoveFromExample);
                }
            }
        }

        foreach (var p in reqParams)
            op.Parameters.Add(p);

        //remove request body if this is a GET request (swagger ui/fetch client doesn't support GET with body)
        //or if there are no properties left on the request dto after above operations
        //only if the request dto is not a list
        if (isGETRequest ||
           (op.RequestBody?.Content.SelectMany(c => c.Value.Schema.ActualSchema.ActualProperties).Any() == false &&
           !op.RequestBody.Content.Where(c => c.Value.Schema.ActualSchema.InheritedSchema is not null).SelectMany(c => c.Value.Schema.ActualSchema.InheritedSchema.ActualProperties).Any() &&
           !op.RequestBody.Content.SelectMany(c => c.Value.Schema.ActualSchema.AllOf.SelectMany(s => s.Properties)).Any()))
        {
            if (reqDtoIsList is false)
            {
                op.RequestBody = null;
                foreach (var body in op.Parameters.Where(x => x.Kind == OpenApiParameterKind.Body).ToArray())
                    op.Parameters.Remove(body);
            }
        }

        if (removeEmptySchemas)
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
                op.Parameters.Add(new OpenApiParameter
                {
                    Name = fromBodyProp.Name,
                    IsRequired = true,
                    Schema = ctx.SchemaGenerator.Generate(fromBodyProp.PropertyType, ctx.SchemaResolver),
                    Kind = OpenApiParameterKind.Body,
                    Description = reqParamDescriptions.GetValueOrDefault(fromBodyProp.Name),
                    Default = fromBodyProp.GetCustomAttribute<DefaultValueAttribute>()?.Value,
                    Example = fromBodyProp.GetExample()
                });
            }
        }

        //set request example if provided by user
        if (epDef.EndpointSummary?.ExampleRequest is not null)
        {
            foreach (var requestBody in op.Parameters.Where(x => x.Kind == OpenApiParameterKind.Body))
            {
                if (epDef.EndpointSummary.ExampleRequest.GetType().IsAssignableTo(typeof(IEnumerable)))
                {
                    requestBody.ActualSchema.Example = JToken.FromObject(epDef.EndpointSummary.ExampleRequest, serializer);
                }
                else
                {
                    var jObj = JObject.FromObject(epDef.EndpointSummary.ExampleRequest, serializer);

                    foreach (var p in jObj.Properties().ToArray())
                    {
                        if (propsToRemoveFromExample.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                            p.Remove();
                    }

                    requestBody.ActualSchema.Example = jObj;
                }
            }
        }
        return true;
    }

    private static string ActualParamName(string input)
    {
        var index = input.IndexOfAny(new[] { '=', ':' });
        index = index == -1 ? input.Length : index;
        var left = input[..index];
        return left.TrimEnd('?');
    }

    private static bool ShouldAddQueryParam(PropertyInfo prop, List<OpenApiParameter> reqParams, bool isGETRequest)
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
            (isGETRequest && !reqParams.Any(rp => rp.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase)))
                ||
            //this prop is marked with [QueryParam], so add. applies to all verbs.
            prop.IsDefined(Types.QueryParamAttribute);
    }

    private static void RemovePropFromRequestBodyContent(string propName, IDictionary<string, OpenApiMediaType>? content, List<string> propsToRemoveFromExample)
    {
        if (content is null) return;

        propsToRemoveFromExample.Add(propName);

        foreach (var c in content)
        {
            var props1 = c.Value.Schema.ActualSchema.ActualProperties;
            var props2 = c.Value.Schema.ActualSchema.AllInheritedSchemas
                .Select(s => s.ActualProperties)
                .SelectMany(s => s.Select(s => s));

            var props = props1.Union(props2);

            var key = props.FirstOrDefault(p => string.Equals(p.Key, propName, StringComparison.OrdinalIgnoreCase)).Key;

            Remove(c.Value.Schema.ActualSchema, key);
        }

        //recursive property removal
        static void Remove(JsonSchema schema, string? key)
        {
            if (key is null) return;

            schema.Properties.Remove(key);

            foreach (var s in schema.AllOf.Union(schema.AllInheritedSchemas))
            {
                Remove(s, key);
            }
        }
    }

    private static string StripRouteConstraints(string relativePath)
    {
        var parts = relativePath.Split('/');

        for (var i = 0; i < parts.Length; i++)
        {
            var p = ActualParamName(parts[i]);

            if (p.StartsWith("{") && !p.EndsWith("}"))
                p += "}";

            parts[i] = p;
        }

        return string.Join("/", parts);
    }

    private static string TagName(string input, TagCase tagCase)
    {
        return tagCase switch
        {
            TagCase.None => input,
            TagCase.TitleCase => textInfo.ToTitleCase(input),
            TagCase.LowerCase => textInfo.ToLower(input),
            _ => input,
        };
    }
}