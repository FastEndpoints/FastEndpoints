using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FastEndpoints.Swashbuckle;

internal class DefaultOperationFilter : IOperationFilter
{
    private static readonly Regex regex = new(@"(?<=\{)[^}]*(?=\})", RegexOptions.Compiled);

    public void Apply(OpenApiOperation op, OperationFilterContext ctx)
    {
        var reqDtoType = ctx.ApiDescription.ParameterDescriptions.FirstOrDefault()?.Type;
        var reqDtoProps = reqDtoType?.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        var isGETRequest = ctx.ApiDescription.HttpMethod == "GET";

        if (isGETRequest && op.RequestBody is not null)
            op.RequestBody.Required = false;

        //add a param for each url path segment such as /{xxx}/{yyy}
        var reqParams = regex
            .Matches(ctx.ApiDescription?.RelativePath!)
            .Select(m => new OpenApiParameter
            {
                Name = m.Value,
                In = ParameterLocation.Path,
                Required = true,
                Schema = new() { Type = "string" }
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
                        Required = false,
                        Schema = new() { Type = p.PropertyType.Name.ToLowerInvariant() },
                        In = ParameterLocation.Query
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
                        Required = attrib?.IsRequired ?? false,
                        Schema = new() { Type = prop.PropertyType.Name.ToLowerInvariant() },
                        In = ParameterLocation.Header
                    });
                }
            }
        }

        //todo: this abomination of a method needs to be refactored!
    }
}