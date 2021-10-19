using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.RegularExpressions;

namespace FastEndpoints.SwaggerExtensions;

/// <summary>
/// a set of extension methods to adding swagger support
/// </summary>
public static class Extensions
{
    /// <summary>
    /// enable support for FastEndpoints in swagger
    /// </summary>
    public static void EnableFastEndpoints(this SwaggerGenOptions options)
    {
        options.CustomSchemaIds(type => type.FullName);
        options.TagActionsBy(d => new[] { d.RelativePath?.Split('/')[0] });
        options.OperationFilter<SwaggerOperationFilter>();
    }

    /// <summary>
    /// enable jwt bearer authorization support
    /// </summary>
    public static void EnableJWTBearerAuth(this SwaggerGenOptions options)
    {
        options.AddSecurityDefinition("bearerAuth", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT Authorization header using the Bearer scheme."
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "bearerAuth"
                    }
                },
                Array.Empty<string>()
            }
        });
    }
}

internal class SwaggerOperationFilter : IOperationFilter
{
    private static readonly Regex regex = new(@"(?<=\{)[^}]*(?=\})", RegexOptions.Compiled);

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
#pragma warning disable CS8604
        var parms = regex
            .Matches(context.ApiDescription.RelativePath)
            .Select(m => new OpenApiParameter
            {
                Name = m.Value,
                In = ParameterLocation.Path,
                Required = true
            });
#pragma warning restore CS8604

        foreach (var p in parms)
            operation.Parameters.Add(p);
    }
}