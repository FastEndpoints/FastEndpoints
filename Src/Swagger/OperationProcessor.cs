﻿using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using NJsonSchema;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FastEndpoints.Swagger;

internal class OperationProcessor : IOperationProcessor
{
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
        { "405", "Mehtod Not Allowed" },
        { "406", "Not Acceptable" },
        { "500", "Server Error" },
    };

    private readonly int tagIndex;
    public OperationProcessor(int tagIndex) => this.tagIndex = tagIndex;

    public bool Process(OperationProcessorContext ctx)
    {
        var metaData = ((AspNetCoreOperationProcessorContext)ctx).ApiDescription.ActionDescriptor.EndpointMetadata;
        var endpoint = metaData.OfType<EndpointDefinition>().SingleOrDefault();

        if (endpoint is null)
            return true; //this is not a fastendpoint

        var apiDescription = ((AspNetCoreOperationProcessorContext)ctx).ApiDescription;
        var opPath = ctx.OperationDescription.Path = $"/{StripRouteConstraints(apiDescription.RelativePath!)}";//fix missing path parameters
        var apiVer = endpoint.Version.Current;
        var version = $"/{Config.VersioningOpts?.Prefix}{apiVer}";
        var routePrefix = "/" + (Config.RoutingOpts?.Prefix ?? "_");
        var bareRoute = opPath.Remove(routePrefix).Remove(version);
        var nameMetaData = metaData.OfType<EndpointNameMetadata>().LastOrDefault();
        var op = ctx.OperationDescription.Operation;

        //set operation id if user has specified
        if (nameMetaData is not null)
            op.OperationId = nameMetaData.EndpointName;

        //set operation tag
        if (tagIndex > 0 && !endpoint.DontAutoTag)
        {
            var segments = bareRoute.Split('/').Where(s => s != string.Empty).ToArray();
            if (segments.Length >= tagIndex)
            {
                op.Tags.Add(segments[tagIndex - 1]);
            }
        }

        //this will be later removed from document processor. this info is needed by the document processor.
        op.Tags.Add($"|{ctx.OperationDescription.Method}:{bareRoute}|{apiVer}|{endpoint.Version.DeprecatedAt}");

        //fix request content-types not displaying correctly
        var reqContent = op.RequestBody?.Content;
        if (reqContent?.Count > 0)
        {
            var contentVal = reqContent.FirstOrDefault().Value;
            var list = new List<KeyValuePair<string, OpenApiMediaType>>(op.Consumes.Count);
            for (int i = 0; i < op.Consumes.Count; i++)
                list.Add(new(op.Consumes[i], contentVal));
            reqContent.Clear();
            foreach (var c in list)
                reqContent.Add(c);
        }

        //fix response content-types not displaying correctly
        if (op.Responses.Count > 0)
        {
            var metas = metaData
             .OfType<IProducesResponseTypeMetadata>()
             .GroupBy(m => m.StatusCode, (k, g) => new { key = k.ToString(), cTypes = g.Last().ContentTypes })
             .ToDictionary(x => x.key);

            if (metas.Count > 0)
            {
                foreach (var resp in op.Responses)
                {
                    var cTypes = metas[resp.Key].cTypes;
                    var mediaType = resp.Value.Content.FirstOrDefault().Value;
                    resp.Value.Content.Clear();
                    foreach (var ct in cTypes)
                        resp.Value.Content.Add(new(ct, mediaType));
                }
            }
        }

        //set endpoint summary & description
        op.Summary = endpoint.Summary?.Summary;
        op.Description = endpoint.Summary?.Description;

        //set response descriptions (no xml comments support here, yet!)
        op.Responses
          .Where(r => string.IsNullOrWhiteSpace(r.Value.Description))
          .ToList()
          .ForEach(res =>
          {
              if (defaultDescriptions.ContainsKey(res.Key))
                  res.Value.Description = defaultDescriptions[res.Key]; //first set the default text

              var key = Convert.ToInt32(res.Key);

              if (endpoint.Summary?.Responses.ContainsKey(key) is true)
                  res.Value.Description = endpoint.Summary.Responses[key]; //then take values from summary object
          });

        var reqDtoType = apiDescription.ParameterDescriptions.FirstOrDefault()?.Type;
        var reqDtoProps = reqDtoType?.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
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
                {
                    reqParamDescriptions[prop.Key] = prop.Value.Description;
                }
            }
        }

        //also add descriptions from user supplied summary request params overriding the above
        if (endpoint.Summary is not null)
        {
            foreach (var param in endpoint.Summary.Params)
            {
                reqParamDescriptions[param.Key] = param.Value;
            }
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

        //add a path param for each route param such as /{xxx}/{yyy}/{zzz}
        reqParams = regex
            .Matches(apiDescription?.RelativePath!)
            .Select(m =>
            {
                //remove corresponding json field from the request body
                RemovePropFromRequestBodyContent(m.Value, op.RequestBody?.Content);

                var pType = reqDtoProps?.SingleOrDefault(p =>
                {
                    var pName = p.GetCustomAttribute<BindFromAttribute>()?.Name ?? p.Name;
                    return string.Equals(pName, m.Value, StringComparison.OrdinalIgnoreCase);
                })?.PropertyType ?? Types.String;

                return new OpenApiParameter
                {
                    Name = ActualParamName(m.Value),
                    Kind = OpenApiParameterKind.Path,
                    IsRequired = true,
                    Schema = JsonSchema.FromType(pType),
                    Description = reqParamDescriptions.GetValueOrDefault(ActualParamName(m.Value))
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
                    var pName = p.GetCustomAttribute<BindFromAttribute>()?.Name ?? p.Name;

                    //remove corresponding json field from the request body
                    RemovePropFromRequestBodyContent(pName, op.RequestBody?.Content);

                    return new OpenApiParameter
                    {
                        Name = pName,
                        IsRequired = !p.IsNullable(),
                        Schema = JsonSchema.FromType(p.PropertyType),
                        Kind = OpenApiParameterKind.Query,
                        Description = reqParamDescriptions.GetValueOrDefault(p.Name)
                    };
                })
                .ToList();

            if (qParams?.Count > 0)
                reqParams.AddRange(qParams);
        }

        if (reqDtoProps is not null)
        {
            foreach (var prop in reqDtoProps)
            {
                foreach (var attribute in prop.GetCustomAttributes())
                {
                    //add header params if there are any props marked with [FromHeader] attribute
                    if (attribute is FromHeaderAttribute hAttrib)
                    {
                        var pName = hAttrib.HeaderName ?? prop.Name;
                        reqParams.Add(new OpenApiParameter
                        {
                            Name = pName,
                            IsRequired = hAttrib.IsRequired,
                            Schema = JsonSchema.FromType(prop.PropertyType),
                            Kind = OpenApiParameterKind.Header,
                            Description = reqParamDescriptions.GetValueOrDefault(pName)
                        });

                        //remove corresponding json body field if it's required. allow binding only from header.
                        if (hAttrib.IsRequired)
                            RemovePropFromRequestBodyContent(prop.Name, op.RequestBody?.Content);
                    }

                    //can only be bound from claim since it's required. so remove prop from body.
                    if (attribute is FromClaimAttribute cAttrib && cAttrib.IsRequired)
                        RemovePropFromRequestBodyContent(prop.Name, op.RequestBody?.Content);

                    //can only be bound from permission since it's required. so remove prop from body.
                    if (attribute is HasPermissionAttribute pAttrib && pAttrib.IsRequired)
                        RemovePropFromRequestBodyContent(prop.Name, op.RequestBody?.Content);
                }
            }
        }

        foreach (var p in reqParams)
            op.Parameters.Add(p);

        if (isGETRequest)
        {
            //remove request body since this is a get request
            //cause swagger ui/fetch client doesn't support GET with body
            op.RequestBody = null;
        }

        //remove request body if there are no properties left after above operations
        //otherwise there's gonna be an empty schema added in the swagger doc
        if (op.RequestBody?.Content.SelectMany(c => c.Value.Schema.ActualSchema.ActualProperties).Any() == false &&
           !op.RequestBody.Content.SelectMany(c => c.Value.Schema.ActualSchema.AllOf.SelectMany(s => s.Properties)).Any())
        {
            op.RequestBody = null;

            //the following still does not remove the empty request schema :-(
            apiDescription?.ParameterDescriptions.Clear();
            var bodyParam = op.Parameters.FirstOrDefault(p => p.Kind == OpenApiParameterKind.Body);
            if (bodyParam != null) op.Parameters.Remove(bodyParam);
        }

        //set request example if provided by user
        if (endpoint.Summary?.ExampleRequest is not null)
        {
            foreach (var requestBody in op.Parameters.Where(x => x.Kind == OpenApiParameterKind.Body))
            {
                //todo: need to figure out how to do the following:
                // 1) read correct property name from the `BindFromAttribute` if present on properties
                // 2) remove properties that are not present in requestBody

                //need to serialize to a json string because just setting the object doesn't honor the serializer settings provided by user.
                requestBody.ActualSchema.Example = Newtonsoft.Json.JsonConvert.SerializeObject(
                    endpoint.Summary.ExampleRequest,
                    ctx.SchemaGenerator.Settings.ActualSerializerSettings);

            }
        }

        return true;
    }

    private static string ActualParamName(string input)
    {
        var index = input.IndexOf(':');
        index = index == -1 ? input.Length : index;
        var left = input[..index];
        return left.TrimEnd('?');
    }

    private static bool ShouldAddQueryParam(PropertyInfo prop, List<OpenApiParameter> reqParams, bool isGETRequest)
    {
        if (!prop.CanWrite)
            return false;

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

    private static void RemovePropFromRequestBodyContent(string propName, IDictionary<string, OpenApiMediaType>? content)
    {
        if (content is null) return;

        propName = ActualParamName(propName);

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

        for (int i = 0; i < parts.Length; i++)
        {
            var p = ActualParamName(parts[i]);

            if (p.StartsWith("{") && !p.EndsWith("}"))
                p += "}";

            parts[i] = p;
        }

        return string.Join("/", parts);
    }
}