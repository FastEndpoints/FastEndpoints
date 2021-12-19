using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FastEndpoints.Swagger;

internal class DefaultOperationFilter : IOperationFilter
{
    private static readonly Regex regex = new(@"(?<=\{)[^}]*(?=\})", RegexOptions.Compiled);

    public void Apply(OpenApiOperation op, OperationFilterContext ctx)
    {
        //var resDtoType = op.RequestBody?.Content.FirstOrDefault().Value.Schema.Reference?.Id.Split(".").LastOrDefault();

        var reqDtoType = ctx.ApiDescription.ParameterDescriptions.FirstOrDefault()?.Type;

        var isGETRequest = ctx.ApiDescription.HttpMethod == "GET";

        if (isGETRequest && op.RequestBody != null)
            op.RequestBody.Required = false;

        var parms = regex
            .Matches(ctx.ApiDescription?.RelativePath!)
            .Select(m => new OpenApiParameter
            {
                Name = m.Value,
                In = ParameterLocation.Path,
                Required = true
            });

        if (isGETRequest && !parms.Any() && reqDtoType != null)
        {
            parms = reqDtoType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .Select(p => new OpenApiParameter
                {
                    Name = p.Name,
                    Required = false,
                    Schema = new() { Type = p.PropertyType.Name },
                    In = ParameterLocation.Query
                });
        }

        foreach (var p in parms)
            op.Parameters.Add(p);
    }
}