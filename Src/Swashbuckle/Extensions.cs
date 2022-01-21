using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace FastEndpoints.Swashbuckle;

/// <summary>
/// a set of extension methods for adding swagger support
/// </summary>
public static class Extensions
{
    private static readonly Dictionary<string, string[]> docToGroupNamesMap = new();

    /// <summary>
    /// enable support for FastEndpoints in swagger
    /// </summary>
    /// <param name="tagIndex">the index of the route path segment to use for tagging/grouping endpoints</param>
    public static void EnableFastEndpoints(this SwaggerGenOptions options, int tagIndex)
    {
        options.CustomSchemaIds(type => type.FullName);
        //options.TagActionsBy(d => new[] { d.RelativePath?.Split('/')[tagIndex] });
        options.OperationFilter<DefaultOperationFilter>(tagIndex);
    }

    /// <summary>
    /// enable jwt bearer authorization support
    /// </summary>
    public static void EnableJWTBearerAuth(this SwaggerGenOptions options)
    {
        options.AddSecurityDefinition("BearerAuth", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter a JWT token to authorize the requests..."
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "BearerAuth"
                    }
                },
                Array.Empty<string>()
            }
        });
    }

    /// <summary>
    /// enable swagger support for FastEndpoints with a single call.
    /// </summary>
    /// <param name="options">swaggergen config options</param>
    /// <param name="serializerOptions">json serializer options</param>
    /// <param name="addJWTBearerAuth">set to false to disable auto addition of jwt bearer auth support</param>
    /// <param name="tagIndex">the index of the route path segment to use for tagging/grouping endpoints</param>
    public static IServiceCollection AddSwashbuckle(this IServiceCollection services,
        Action<SwaggerGenOptions>? options = null,
        Action<JsonOptions>? serializerOptions = null,
        bool addJWTBearerAuth = true,
        int tagIndex = 1)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.EnableFastEndpoints(tagIndex);
            if (addJWTBearerAuth) o.EnableJWTBearerAuth();
            options?.Invoke(o);
        });

        if (serializerOptions is not null)
            services.AddMvcCore().AddJsonOptions(serializerOptions);
        else
            services.AddMvcCore().AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null);

        return services;
    }

    /// <summary>
    /// configure swagger ui with some sensible defaults for FastEndpoints which can be overridden if needed.
    /// </summary>
    public static void ConfigureDefaults(this SwaggerUIOptions o)
    {
        o.ConfigObject.DocExpansion = DocExpansion.None;
        o.ConfigObject.DefaultModelExpandDepth = 0;
        o.ConfigObject.Filter = "";
        o.ConfigObject.PersistAuthorization = true;
        o.ConfigObject.DisplayRequestDuration = true;
        o.ConfigObject.TryItOutEnabled = true;
        o.ConfigObject.AdditionalItems["tagsSorter"] = "alpha";
        o.ConfigObject.AdditionalItems["operationsSorter"] = "alpha";
    }

    public static void SwaggerDoc(this SwaggerGenOptions opts, string documentName, OpenApiInfo info, string[] apiGroupNames)
    {
        opts.SwaggerGeneratorOptions.SwaggerDocs.Add(documentName, info);
        docToGroupNamesMap[documentName] = apiGroupNames;

        opts.DocInclusionPredicate((docName, apiDesc) =>
        {
            return docToGroupNamesMap[docName].Contains(apiDesc.GroupName);
        });
    }

    internal static string Remove(this string value, string removeString)
    {
        int index = value.IndexOf(removeString, StringComparison.Ordinal);
        return index < 0 ? value : value.Remove(index, removeString.Length);
    }
}