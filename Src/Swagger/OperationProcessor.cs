using Microsoft.AspNetCore.Http.Metadata;
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
        var op = ctx.OperationDescription.Operation;
        var metaData = ((AspNetCoreOperationProcessorContext)ctx).ApiDescription.ActionDescriptor.EndpointMetadata;
        var epMeta = metaData.OfType<EndpointMetadata>().SingleOrDefault();

        if (epMeta is null)
            return true; //this is not a fastendpoint

        var apiVer = epMeta.EndpointSettings.Version.Current;
        var version = $"/{Config.VersioningOpts?.Prefix}{apiVer}";
        var routePrefix = "/" + (Config.RoutingOpts?.Prefix ?? "_");
        var bareRoute = ctx.OperationDescription.Path.Remove(routePrefix).Remove(version);
        var nameMetaData = metaData.OfType<EndpointNameMetadata>().LastOrDefault();

        //set operation id if user has specified
        if (nameMetaData is not null)
            op.OperationId = nameMetaData.EndpointName;

        //set operation tag
        if (tagIndex > 0)
        {
            var segments = bareRoute.Split('/');
            if (segments.Length >= tagIndex)
                op.Tags.Add(segments[tagIndex]);
        }

        //this will be later removed from document processor
        op.Tags.Add($"|{ctx.OperationDescription.Method}:{bareRoute}|{apiVer}|{epMeta.EndpointSettings.Version.DeprecatedAt}");

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
        op.Summary = epMeta.EndpointSettings.Summary?.Summary;
        op.Description = epMeta.EndpointSettings.Summary?.Description;

        //set response descriptions (no xml comments support here, yet!)
        op.Responses
          .Where(r => string.IsNullOrWhiteSpace(r.Value.Description))
          .ToList()
          .ForEach(res =>
          {
              if (defaultDescriptions.ContainsKey(res.Key))
                  res.Value.Description = defaultDescriptions[res.Key]; //first set the default text

              if (epMeta.EndpointSettings.Summary is not null)
                  res.Value.Description = epMeta.EndpointSettings.Summary.Responses.GetValueOrDefault(Convert.ToInt32(res.Key)); //then take values from summary object
          });

        var apiDescription = ((AspNetCoreOperationProcessorContext)ctx).ApiDescription;
        var reqDtoType = apiDescription.ParameterDescriptions.FirstOrDefault()?.Type;
        var reqDtoProps = reqDtoType?.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        var isGETRequest = apiDescription.HttpMethod == "GET";

        //fix missing path parameters
        ctx.OperationDescription.Path = "/" + apiDescription.RelativePath;

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
        if (epMeta.EndpointSettings.Summary is not null)
        {
            foreach (var param in epMeta.EndpointSettings.Summary.Params)
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
                    {
                        prop.Value.Description = reqParamDescriptions[prop.Key];
                    }
                }

                if (c.Value.Schema.ActualSchema.InheritedSchema is not null)
                {
                    foreach (var prop in c.Value.Schema.ActualSchema.InheritedSchema.ActualProperties)
                    {
                        if (reqParamDescriptions.ContainsKey(prop.Key))
                        {
                            prop.Value.Description = reqParamDescriptions[prop.Key];
                        }
                    }
                }
            }
        }

        if (isGETRequest && op.RequestBody is not null)
        {
            //remove request body since this is a get request with a request dto,
            //cause swagger ui/fetch client doesn't support GET with body
            op.RequestBody = null;
        }

        var reqParams = new List<OpenApiParameter>();

        //add a param for each route param such as /{xxx}/{yyy}/{zzz}
        reqParams = regex
            .Matches(apiDescription?.RelativePath!)
            .Select(m =>
            {
                if (op.RequestBody is not null)
                {
                    //remove corresponding json field from the request body
                    foreach (var c in op.RequestBody.Content)
                    {
                        var prop = c.Value.Schema.ActualSchema.ActualProperties.FirstOrDefault(kvp =>
                            string.Equals(kvp.Key, ActualParamName(m.Value), StringComparison.OrdinalIgnoreCase));

                        if (prop.Key != null)
                        {
                            c.Value.Schema.ActualSchema.Properties.Remove(prop.Key);

                            foreach (var schema in c.Value.Schema.ActualSchema.AllOf)
                                schema.Properties.Remove(prop.Key);
                        }
                    }
                }

                return new OpenApiParameter
                {
                    Name = ActualParamName(m.Value),
                    Kind = OpenApiParameterKind.Path,
                    IsRequired = !m.Value.EndsWith('?'),
                    Schema = JsonSchema.FromType(typeof(string)),
                    Description = reqParamDescriptions.GetValueOrDefault(ActualParamName(m.Value))
                };
            })
            .ToList();

        if (isGETRequest && reqDtoType is not null)
        {
            //it's a GET request with a request dto
            //so let's add each dto property as a query param to enable swagger ui to execute GET request with user supplied values

            var qParams = reqDtoProps?
                .Where(p =>
                      !p.IsDefined(typeof(FromClaimAttribute), false) &&
                      !p.IsDefined(typeof(FromHeaderAttribute), false) &&
                      !reqParams.Any(rp => rp.Name == p.Name)) //ignore props marksed with [FromClaim],[FromHeader] or has a route param.
                .Select(p =>
                    new OpenApiParameter
                    {
                        Name = p.Name,
                        IsRequired = Nullable.GetUnderlyingType(p.PropertyType) == null ? false : true,
                        Schema = JsonSchema.FromType(p.PropertyType),
                        Kind = OpenApiParameterKind.Query,
                        Description = reqParamDescriptions.GetValueOrDefault(p.Name)
                    })
                .ToList();

            if (qParams?.Count > 0)
                reqParams.AddRange(qParams);
        }

        //add header params if there are any props marked with [FromHeader] attribute
        if (reqDtoProps is not null)
        {
            foreach (var prop in reqDtoProps)
            {
                var attrib = prop.GetCustomAttribute<FromHeaderAttribute>(true);
                if (attrib is not null)
                {
                    var pName = attrib?.HeaderName ?? prop.Name;
                    reqParams.Add(new OpenApiParameter
                    {
                        Name = pName,
                        IsRequired = attrib?.IsRequired ?? false,
                        Schema = JsonSchema.FromType(prop.PropertyType),
                        Kind = OpenApiParameterKind.Header,
                        Description = reqParamDescriptions.GetValueOrDefault(pName)
                    });
                }
            }
        }

        foreach (var p in reqParams)
            op.Parameters.Add(p);

        return true;

        static string ActualParamName(string input)
        {
            var index = input.IndexOf(':');
            index = index == -1 ? input.Length : index;
            var left = input[..index];
            return left.TrimEnd('?');
        }
    }
}