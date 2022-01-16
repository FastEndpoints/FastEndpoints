using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSwag;
using NSwag.AspNetCore;
using NSwag.Generation;
using NSwag.Generation.Processors.Security;

namespace FastEndpoints.NSwag;

/// <summary>
/// a set of extension methods for adding swagger support
/// </summary>
public static class Extensions
{
    /// <summary>
    /// enable support for FastEndpoints in swagger
    /// </summary>
    public static void EnableFastEndpoints(this OpenApiDocumentGeneratorSettings settings)
    {
        settings.Title = AppDomain.CurrentDomain.FriendlyName;
        settings.SchemaNameGenerator = new DefaultSchemaNameGenerator();
        settings.OperationProcessors.Add(new DefaultOperationProcessor());
    }

    /// <summary>
    /// enable jwt bearer authorization support
    /// </summary>
    public static void EnableJWTBearerAuth(this OpenApiDocumentGeneratorSettings settings)
    {
        settings.AddSecurity("JWTBearerAuth", new OpenApiSecurityScheme
        {
            Type = OpenApiSecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter a JWT token to authorize the requests..."
        });

        settings.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("JWTBearerAuth"));
    }

    /// <summary>
    /// enable swagger support for FastEndpoints with a single call.
    /// </summary>
    /// <param name="settings">swaggergen config settings</param>
    /// <param name="serializerOptions">json serializer options</param>
    /// <param name="addJWTBearerAuth">set to false to disable auto addition of jwt bearer auth support</param>
    public static IServiceCollection AddNSwag(this IServiceCollection services,
        Action<OpenApiDocumentGeneratorSettings>? settings = null,
        Action<JsonOptions>? serializerOptions = null,
        bool addJWTBearerAuth = true)
    {
        services.AddEndpointsApiExplorer();
        services.AddOpenApiDocument(s =>
        {
            s.EnableFastEndpoints();
            if (addJWTBearerAuth) s.EnableJWTBearerAuth();
            settings?.Invoke(s);
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
    public static void ConfigureDefaults(this SwaggerUi3Settings s)
    {
        s.AdditionalSettings["filter"] = true;
        s.AdditionalSettings["persistAuthorization"] = true;
        s.AdditionalSettings["displayRequestDuration"] = true;
        s.AdditionalSettings["tryItOutEnabled"] = true;
        s.TagsSorter = "alpha";
        s.OperationsSorter = "alpha";
        s.CustomInlineStyles = ".servers-title,.servers{display:none} .swagger-ui .info{margin:10px 0} .swagger-ui .scheme-container{margin:10px 0;padding:10px 0} .swagger-ui .info .title{font-size:25px} .swagger-ui textarea{min-height:150px}";
    }
}