using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FastEndpoints.Swashbuckle;

internal class DefaultOperationFilter : IOperationFilter
{
    private static readonly Regex regex = new(@"(?<=\{)[^}]*(?=\})", RegexOptions.Compiled);

    private readonly int tagIndex;
    public DefaultOperationFilter(int tagIndex) => this.tagIndex = tagIndex;

    public void Apply(OpenApiOperation op, OperationFilterContext ctx)
    {
        var reqDtoType = ctx.ApiDescription.ParameterDescriptions.FirstOrDefault()?.Type;
        var reqDtoProps = reqDtoType?.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        var isGETRequest = ctx.ApiDescription.HttpMethod == "GET";

        //use tagIndex to determine a tag for the endpoint
        op.Tags.Remove(op.Tags.Single(t => t.Name == nameof(EndpointExecutor)));
        if (op.Tags.Count == 0)
        {
            var routePrefix = "/" + (Config.RoutingOpts?.Prefix ?? "_");
            var version = "/" + (ctx.ApiDescription.GroupName ?? "_");
            var segments = ctx.ApiDescription.RelativePath?.Remove(routePrefix).Remove(version).Split('/');
            if (segments?.Length >= tagIndex) op.Tags.Add(new() { Name = segments[tagIndex] });
        }

        if (isGETRequest && op.RequestBody is not null)
        {
            //remove request body since this is a get request with a request dto cause swagger ui/fetch client doesn't support GET with body
            op.RequestBody = null;
        }

        var reqParams = new List<OpenApiParameter>();

        //add a param for each url path segment such as /{xxx}/{yyy}/{yyy}
        reqParams = regex
            .Matches(ctx.ApiDescription?.RelativePath!)
            .Select(m => new OpenApiParameter
            {
                Name = m.Value,
                In = ParameterLocation.Path,
                Required = true,
                Schema = new() { Type = "string" }
            })
            .ToList();

        if (isGETRequest && reqDtoType is not null)
        {
            //it's a GET request with a request dto
            //so let's add each dto property as a query param to enable swagger ui to execute GET request with user supplied values

            var qParams = reqDtoProps?
                .Where(p =>
                      !p.IsDefined(typeof(FromClaimAttribute), false) &&
                      !p.IsDefined(typeof(FromHeaderAttribute), false)) //ignore props marks with [FromClaim] and [FromHeader]
                .Select(p =>
                    new OpenApiParameter
                    {
                        Name = p.Name,
                        Required = false,
                        Schema = new() { Type = p.PropertyType.Name.ToLowerInvariant() },
                        In = ParameterLocation.Query
                    })
                .ToList();

            if (qParams?.Count > 0)
                reqParams.AddRange(qParams);
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

        foreach (var p in reqParams)
            op.Parameters.Add(p);
    }
}