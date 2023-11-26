using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Namotion.Reflection;
using NJsonSchema;
using NJsonSchema.Generation;
using NSwag;
using NSwag.AspNetCore;
using NSwag.Generation;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors.Contexts;
using NSwag.Generation.Processors.Security;
using System.Reflection;
using System.Text.Json;
using Newtonsoft.Json;
using NJsonSchema.NewtonsoftJson.Generation;

namespace FastEndpoints.Swagger;

/// <summary>
/// a set of extension methods for adding swagger support
/// </summary>
public static class Extensions
{
    /// <summary>
    /// JsonNamingPolicy chosen for swagger
    /// </summary>
    public static JsonNamingPolicy? SelectedJsonNamingPolicy { get; private set; }

    /// <summary>
    /// enable support for FastEndpoints and create a swagger document.
    /// </summary>
    /// <param name="options">swagger document configuration options</param>
    public static IServiceCollection SwaggerDocument(this IServiceCollection services, Action<DocumentOptions>? options = null)
    {
        var doc = new DocumentOptions();
        options?.Invoke(doc);
        services.AddEndpointsApiExplorer();
        services.AddOpenApiDocument(
            genSettings =>
            {
                var stjOpts = new JsonSerializerOptions(Conf.SerOpts.Options);
                SelectedJsonNamingPolicy = stjOpts.PropertyNamingPolicy;
                doc.SerializerSettings?.Invoke(stjOpts);
                var newtonsoftOpts = SystemTextJsonUtilities.ConvertJsonOptionsToNewtonsoftSettings(stjOpts);
                doc.NewtonsoftSettings?.Invoke(newtonsoftOpts);
                genSettings.SchemaSettings = new NewtonsoftJsonSchemaGeneratorSettings
                {
                    SerializerSettings = newtonsoftOpts,
                    SchemaType = SchemaType.OpenApi3
                };

                EnableFastEndpoints(genSettings, doc);

                if (doc.EndpointFilter is not null)
                    genSettings.OperationProcessors.Insert(0, new EndpointFilter(doc.EndpointFilter));
                if (doc.ExcludeNonFastEndpoints)
                    genSettings.OperationProcessors.Insert(0, new FastEndpointsFilter());

                if (doc.TagDescriptions is not null)
                {
                    var dict = new Dictionary<string, string>();
                    doc.TagDescriptions(dict);
                    genSettings.AddOperationFilter(
                        ctx =>
                        {
                            foreach (var kvp in dict)
                            {
                                ctx.Document.Tags.Add(
                                    new()
                                    {
                                        Name = kvp.Key,
                                        Description = kvp.Value
                                    });
                            }

                            return true;
                        });
                }
                if (doc.EnableJWTBearerAuth)
                    genSettings.EnableJWTBearerAuth();
                doc.DocumentSettings?.Invoke(genSettings);
                if (doc.RemoveEmptyRequestSchema || doc.FlattenSchema)
                    genSettings.SchemaSettings.FlattenInheritanceHierarchy = true;
            });

        return services;
    }

    /// <summary>
    /// enables the open-api/swagger middleware for fastendpoints.
    /// this method is simply a shortcut for the two calls [<c>app.UseOpenApi()</c>] and [<c>app.UseSwaggerUi3(c => c.ConfigureDefaults())</c>]
    /// </summary>
    /// <param name="config">optional config action for the open-api middleware</param>
    /// <param name="uiConfig">optional config action for the swagger-ui</param>
    public static IApplicationBuilder UseSwaggerGen(this IApplicationBuilder app,
                                                    Action<OpenApiDocumentMiddlewareSettings>? config = null,
                                                    Action<SwaggerUiSettings>? uiConfig = null)
    {
        app.UseOpenApi(config);
        app.UseSwaggerUi((c => c.ConfigureDefaults()) + uiConfig);

        return app;
    }

    /// <summary>
    /// enable support for FastEndpoints in swagger
    /// </summary>
    /// <param name="documentOptions">the document options</param>
    public static void EnableFastEndpoints(this AspNetCoreOpenApiDocumentGeneratorSettings settings, Action<DocumentOptions> documentOptions)
    {
        var doc = new DocumentOptions();
        documentOptions(doc);
        EnableFastEndpoints(settings, doc);
    }

    /// <summary>
    /// enable jwt bearer authorization support
    /// </summary>
    public static void EnableJWTBearerAuth(this AspNetCoreOpenApiDocumentGeneratorSettings settings)
    {
        settings.AddAuth(
            "JWTBearerAuth",
            new()
            {
                Type = OpenApiSecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                Description = "Enter a JWT token to authorize the requests..."
            });
    }

    /// <summary>
    /// configure swagger ui with some sensible defaults for FastEndpoints which can be overridden if needed.
    /// </summary>
    /// <param name="settings">provide an action that overrides any of the defaults</param>
    public static void ConfigureDefaults(this SwaggerUiSettings s, Action<SwaggerUiSettings>? settings = null)
    {
        s.AdditionalSettings["filter"] = true;
        s.AdditionalSettings["persistAuthorization"] = true;
        s.AdditionalSettings["displayRequestDuration"] = true;
        s.AdditionalSettings["tryItOutEnabled"] = true;
        s.TagsSorter = "alpha";
        s.OperationsSorter = "alpha";
        s.CustomInlineStyles =
            ".servers-title,.servers{display:none} .swagger-ui .info{margin:10px 0} .swagger-ui .scheme-container{margin:10px 0;padding:10px 0} .swagger-ui .info .title{font-size:25px} .swagger-ui textarea{min-height:150px}";
        settings?.Invoke(s);
    }

    /// <summary>
    /// the "Try It Out" button is activated by default. call this method to de-activate it by default.
    /// set <see cref="SwaggerUiSettings.EnableTryItOut" /> to <c>false</c> to remove the button from ui.
    /// </summary>
    public static void DeActivateTryItOut(this SwaggerUiSettings s)
        => s.AdditionalSettings.Remove("tryItOutEnabled");

    /// <summary>
    /// add swagger auth for this open api document
    /// </summary>
    /// <param name="schemeName">the authentication scheme</param>
    /// <param name="securityScheme">an open api security scheme object</param>
    /// <param name="globalScopeNames">a collection of global scope names</param>
    /// <returns></returns>
    public static OpenApiDocumentGeneratorSettings AddAuth(this OpenApiDocumentGeneratorSettings s,
                                                           string schemeName,
                                                           OpenApiSecurityScheme securityScheme,
                                                           IEnumerable<string>? globalScopeNames = null)
    {
        if (globalScopeNames is null)
            s.DocumentProcessors.Add(new SecurityDefinitionAppender(schemeName, securityScheme));
        else
            s.DocumentProcessors.Add(new SecurityDefinitionAppender(schemeName, globalScopeNames, securityScheme));

        s.OperationProcessors.Add(new OperationSecurityProcessor(schemeName));

        return s;
    }

    /// <summary>
    /// mark all non-nullable properties of the schema as required in the swagger document.
    /// this may only be needed for TS client generation with OAS3 swagger definitions.
    /// </summary>
    public static void MarkNonNullablePropsAsRequired(this AspNetCoreOpenApiDocumentGeneratorSettings x)
        => x.SchemaSettings.SchemaProcessors.Add(new MarkNonNullablePropsAsRequired());

    /// <summary>
    /// gets the <see cref="EndpointDefinition" /> from the nwag operation processor context if this is a FastEndpoint operation. otherwise returns null.
    /// </summary>
    public static EndpointDefinition? GetEndpointDefinition(this OperationProcessorContext ctx)
        => ((AspNetCoreOperationProcessorContext)ctx)
           .ApiDescription
           .ActionDescriptor
           .EndpointMetadata
           .OfType<EndpointDefinition>()
           .SingleOrDefault();

    /// <summary>
    /// gets the example object if any, from a given <see cref="ProducesResponseTypeMetadata" /> internal class
    /// </summary>
    public static object? GetExampleFromMetaData(this IProducesResponseTypeMetadata metadata)
        => (metadata as ProducesResponseTypeMetadata)?.Example;

    internal static string Remove(this string value, string removeString)
    {
        var index = value.IndexOf(removeString, StringComparison.Ordinal);

        return index < 0 ? value : value.Remove(index, removeString.Length);
    }

    internal static bool HasNoProperties(this IDictionary<string, OpenApiMediaType> content)
        => !content.Any(c => c.GetAllProperties().Any());

    internal static IEnumerable<KeyValuePair<string, JsonSchemaProperty>> GetAllProperties(this KeyValuePair<string, OpenApiMediaType> mediaType)
    {
        return mediaType
               .Value.Schema.ActualSchema.ActualProperties
               .Union(
                   mediaType
                       .Value.Schema.ActualSchema.AllInheritedSchemas
                       .Select(s => s.ActualProperties)
                       .SelectMany(s => s.Select(s => s)));
    }

    internal static IEnumerable<KeyValuePair<string, JsonSchemaProperty>> GetAllProperties(this KeyValuePair<string, OpenApiResponse> response)
    {
        return response
               .Value.Schema.ActualSchema.ActualProperties
               .Union(
                   response.Value.Schema.ActualSchema.AllInheritedSchemas
                           .Select(s => s.ActualProperties)
                           .SelectMany(s => s.Select(s => s)));
    }

    static readonly NullabilityInfoContext _nullCtx = new();

    internal static bool IsNullable(this PropertyInfo p)
        => _nullCtx.Create(p).WriteState == NullabilityState.Nullable;

    internal static string? GetXmlExample(this PropertyInfo p)
    {
        var example = p.GetXmlDocsTag("example");

        return string.IsNullOrEmpty(example) ? null : example;
    }

    internal static string? GetSummary(this Type p)
    {
        var summary = p.GetXmlDocsSummary();

        return string.IsNullOrEmpty(summary) ? null : summary;
    }

    internal static string? GetDescription(this Type p)
    {
        var remarks = p.GetXmlDocsRemarks();

        return string.IsNullOrEmpty(remarks) ? null : remarks;
    }

    internal static string ApplyPropNamingPolicy(this string paramName, DocumentOptions documentOptions)
        => documentOptions.UsePropertyNamingPolicyForParams && SelectedJsonNamingPolicy is not null
               ? SelectedJsonNamingPolicy.ConvertName(paramName)
               : paramName;

    static void EnableFastEndpoints(AspNetCoreOpenApiDocumentGeneratorSettings settings, DocumentOptions opts)
    {
        settings.Title = AppDomain.CurrentDomain.FriendlyName;
        settings.SchemaSettings.SchemaNameGenerator = new SchemaNameGenerator(opts.ShortSchemaNames);
        settings.SchemaSettings.SchemaProcessors.Add(new ValidationSchemaProcessor());
        settings.OperationProcessors.Add(new OperationProcessor(opts));
        settings.DocumentProcessors.Add(new DocumentProcessor(opts.MinEndpointVersion, opts.MaxEndpointVersion, opts.ShowDeprecatedOps));
    }
}