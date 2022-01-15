using NJsonSchema;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FastEndpoints.NSwag;

internal class DefaultOperationProcessor : IOperationProcessor
{
    private static readonly Regex regex = new(@"(?<=\{)[^}]*(?=\})", RegexOptions.Compiled);
    private static readonly Dictionary<string, string> descriptions = new()
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

    public bool Process(OperationProcessorContext ctx)
    {
        var op = ctx.OperationDescription.Operation;

        //use first part of route as tag by default
        var tags = op.Tags;
        if (tags.Count == 0)
            tags.Add(ctx.OperationDescription.Path.Split('/')[1]);

        var reqContent = op.RequestBody?.Content;
        if (reqContent?.Count > 0)
        {
            //fix request content-type not displaying correctly. probably a nswag bug. might be fixed in future.
            var contentVal = reqContent.FirstOrDefault().Value;
            reqContent.Clear();
            reqContent.Add(op.Consumes.FirstOrDefault(), contentVal);
        }

        var resContent = op.Responses.FirstOrDefault().Value.Content;
        if (resContent?.Count > 0)
        {
            //fix response content-type not displaying correctly. probably a nswag bug. might be fixed in future.
            var contentVal = resContent.FirstOrDefault().Value;
            resContent.Clear();
            resContent.Add(op.Produces.FirstOrDefault(), contentVal);
        }

        //set response descriptions
        op.Responses
          .Where(r => string.IsNullOrWhiteSpace(r.Value.Description))
          .ToList()
          .ForEach(res =>
          {
              if (descriptions.ContainsKey(res.Key))
                  res.Value.Description = descriptions[res.Key];
          });

        var apiDescription = ((AspNetCoreOperationProcessorContext)ctx).ApiDescription;
        var reqDtoType = apiDescription.ParameterDescriptions.FirstOrDefault()?.Type;
        var reqDtoProps = reqDtoType?.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        var isGETRequest = apiDescription.HttpMethod == "GET";

        //fix missing path parameters
        ctx.OperationDescription.Path = "/" + apiDescription.RelativePath;

        if (isGETRequest && op.RequestBody is not null)
            op.RequestBody.IsRequired = false;

        //add a param for each url path segment such as /{xxx}/{yyy}
        var reqParams = regex
            .Matches(apiDescription?.RelativePath!)
            .Select(m => new OpenApiParameter
            {
                Name = m.Value,
                Kind = OpenApiParameterKind.Path,
                IsRequired = true,
                Schema = JsonSchema.FromType(typeof(string))
            });

        if (isGETRequest && !reqParams.Any() && reqDtoType is not null)
        {
            //it's a GET request with a request dto and no path params
            //so let's add each dto property as a query param to enable swagger ui to execute GET request with user supplied values

            reqParams = reqDtoProps?
                .Where(p => !p.IsDefined(typeof(FromClaimAttribute), false)) //ignore props marks with [FromClaim]
                .Select(p =>
                    new OpenApiParameter
                    {
                        Name = p.Name,
                        IsRequired = false,
                        Schema = JsonSchema.FromType(p.PropertyType),
                        Kind = OpenApiParameterKind.Query
                    });
        }

        if (reqParams is not null)
        {
            foreach (var p in reqParams)
                op.Parameters.Add(p);
        }

        if (reqDtoProps is not null)
        {
            //add header params if there are any props marked with [FromHeader] attribute
            foreach (var prop in reqDtoProps)
            {
                var attrib = prop.GetCustomAttribute<FromHeaderAttribute>(true);
                if (attrib is not null)
                {
                    op.Parameters.Add(new OpenApiParameter
                    {
                        Name = attrib?.HeaderName ?? prop.Name,
                        IsRequired = attrib?.IsRequired ?? false,
                        Schema = JsonSchema.FromType(prop.PropertyType),
                        Kind = OpenApiParameterKind.Header
                    });
                }
            }
        }

        //var brk = ctx.OperationDescription.Path == "/uploads/image/save-typed";

        return true;
    }
}
