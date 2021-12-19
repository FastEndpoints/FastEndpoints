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
        var reqDtoProps = reqDtoType?.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        var isGETRequest = ctx.ApiDescription.HttpMethod == "GET";

        if (isGETRequest && op.RequestBody != null)
            op.RequestBody.Required = false;

        var parms = regex
            .Matches(ctx.ApiDescription?.RelativePath!)
            .Select(m => new OpenApiParameter
            {
                Name = m.Value,
                In = ParameterLocation.Path,
                Required = true,
                Schema = new() { Type = "string" }
            });

        if (isGETRequest && !parms.Any() && reqDtoType != null)
        {
            parms = reqDtoProps?.Select(p =>
                new OpenApiParameter
                {
                    Name = p.Name,
                    Required = false,
                    Schema = new() { Type = p.PropertyType.Name.ToLowerInvariant() },
                    In = ParameterLocation.Query
                });
        }

        if (parms is not null)
        {
            foreach (var p in parms)
                op.Parameters.Add(p);
        }

        if (reqDtoProps is not null)
        {
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