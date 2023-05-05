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
using System.Runtime.CompilerServices;
using System.Text.Json;

[assembly: InternalsVisibleTo("FastEndpoints.Swagger.UnitTests")]
[assembly: InternalsVisibleTo("FastEndpoints.Swagger.IntegrationTests")]

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
        services.AddOpenApiDocument(generator =>
        {
            var stjOpts = new JsonSerializerOptions(Config.SerOpts.Options);
            SelectedJsonNamingPolicy = stjOpts.PropertyNamingPolicy;
            doc.SerializerSettings?.Invoke(stjOpts);
            generator.SerializerSettings = SystemTextJsonUtilities.ConvertJsonOptionsToNewtonsoftSettings(stjOpts);
            EnableFastEndpoints(generator, doc);
            if (doc.EndpointFilter is not null) generator.OperationProcessors.Insert(0, new EndpointFilter(doc.EndpointFilter));
            if (doc.ExcludeNonFastEndpoints) generator.OperationProcessors.Insert(0, new FastEndpointsFilter());
            if (doc.TagDescriptions is not null)
            {
                var dict = new Dictionary<string, string>();
                doc.TagDescriptions(dict);
                generator.AddOperationFilter(ctx =>
                {
                    foreach (var kvp in dict)
                    {
                        ctx.Document.Tags.Add(new OpenApiTag
                        {
                            Name = kvp.Key,
                            Description = kvp.Value
                        });
                    }
                    return true;
                });
            }
            if (doc.EnableJWTBearerAuth) generator.EnableJWTBearerAuth();
            doc.DocumentSettings?.Invoke(generator);
            if (doc.RemoveEmptyRequestSchema || doc.FlattenSchema) generator.FlattenInheritanceHierarchy = true;
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
                                                    Action<SwaggerUi3Settings>? uiConfig = null)
    {
        app.UseOpenApi(config);
        app.UseSwaggerUi3((c => c.ConfigureDefaults()) + uiConfig);
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
        settings.AddAuth("JWTBearerAuth", new OpenApiSecurityScheme
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
    public static void ConfigureDefaults(this SwaggerUi3Settings s, Action<SwaggerUi3Settings>? settings = null)
    {
        s.AdditionalSettings["filter"] = true;
        s.AdditionalSettings["persistAuthorization"] = true;
        s.AdditionalSettings["displayRequestDuration"] = true;
        s.AdditionalSettings["tryItOutEnabled"] = true;
        s.TagsSorter = "alpha";
        s.OperationsSorter = "alpha";
        s.CustomInlineStyles = ".servers-title,.servers{display:none} .swagger-ui .info{margin:10px 0} .swagger-ui .scheme-container{margin:10px 0;padding:10px 0} .swagger-ui .info .title{font-size:25px} .swagger-ui textarea{min-height:150px}";
        settings?.Invoke(s);
    }

    /// <summary>
    /// the "Try It Out" button is activated by default. call this method to de-activate it by default.
    /// set <see cref="SwaggerUi3Settings.EnableTryItOut"/> to <c>false</c> to remove the button from ui.
    /// </summary>
    public static void DeActivateTryItOut(this SwaggerUi3Settings s)
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
        => x.SchemaProcessors.Add(new MarkNonNullablePropsAsRequired());

    /// <summary>
    /// gets the <see cref="EndpointDefinition"/> from the nwag operation processor context if this is a FastEndpoint operation. otherwise returns null.
    /// </summary>
    public static EndpointDefinition? GetEndpointDefinition(this OperationProcessorContext ctx)
        => ((AspNetCoreOperationProcessorContext)ctx)
            .ApiDescription
            .ActionDescriptor
            .EndpointMetadata
            .OfType<EndpointDefinition>()
            .SingleOrDefault();

    /// <summary>
    /// gets the example object if any, from a given <see cref="ProducesResponseTypeMetadata"/> internal class
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
        return
            mediaType.Value.Schema.ActualSchema.ActualProperties.Union(
                mediaType.Value.Schema.ActualSchema.AllInheritedSchemas
                    .Select(s => s.ActualProperties)
                    .SelectMany(s => s.Select(s => s)));
    }

    internal static IEnumerable<KeyValuePair<string, JsonSchemaProperty>> GetAllProperties(this KeyValuePair<string, OpenApiResponse> response)
    {
        return
            response.Value.Schema.ActualSchema.ActualProperties.Union(
                response.Value.Schema.ActualSchema.AllInheritedSchemas
                    .Select(s => s.ActualProperties)
                    .SelectMany(s => s.Select(s => s)));
    }

    private static readonly NullabilityInfoContext nullCtx = new();
    internal static bool IsNullable(this PropertyInfo p) => nullCtx.Create(p).WriteState == NullabilityState.Nullable;

    internal static string? GetExample(this PropertyInfo p)
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

    private static void EnableFastEndpoints(AspNetCoreOpenApiDocumentGeneratorSettings settings, DocumentOptions opts)
    {
        settings.Title = AppDomain.CurrentDomain.FriendlyName;
        settings.SchemaNameGenerator = new SchemaNameGenerator(opts.ShortSchemaNames);
        settings.SchemaProcessors.Add(new ValidationSchemaProcessor());
        settings.OperationProcessors.Add(new OperationProcessor(opts));
        settings.DocumentProcessors.Add(new DocumentProcessor(opts.MinEndpointVersion, opts.MaxEndpointVersion));
    }

    //todo: remove at next major version
    [Obsolete("Use EnableFastEndpoints(Action<DocumentOptions>) instead!")]
    public static void EnableFastEndpoints(this AspNetCoreOpenApiDocumentGeneratorSettings settings, int tagIndex, TagCase tagCase, int minEndpointVersion, int maxEndpointVersion, bool shortSchemaNames, bool removeEmptySchemas)
    {
        EnableFastEndpoints(settings, new DocumentOptions()
        {
            AutoTagPathSegmentIndex = tagIndex,
            TagCase = tagCase,
            MinEndpointVersion = minEndpointVersion,
            MaxEndpointVersion = maxEndpointVersion,
            ShortSchemaNames = shortSchemaNames,
            RemoveEmptyRequestSchema = removeEmptySchemas
        });
    }

    //todo: remove at next major version
    [Obsolete("Use the EndpointFilter property on the DocumentOptions object at the top level!")]
    public static void EndpointFilter(this AspNetCoreOpenApiDocumentGeneratorSettings x, Func<EndpointDefinition, bool> filter)
        => x.OperationProcessors.Insert(0, new EndpointFilter(filter));

    //todo: remove at next major version
    [Obsolete("Use the SwaggerDocument() method!")]
    public static IServiceCollection AddSwaggerDoc(this IServiceCollection services, Action<AspNetCoreOpenApiDocumentGeneratorSettings>? settings = null, Action<JsonSerializerOptions>? serializerSettings = null, bool addJWTBearerAuth = true, int tagIndex = 1, TagCase tagCase = TagCase.TitleCase, int minEndpointVersion = 0, int maxEndpointVersion = 0, bool shortSchemaNames = false, bool removeEmptySchemas = false, bool excludeNonFastEndpoints = false)
    {
        return SwaggerDocument(
            services: services,
            options: o =>
            {
                o.DocumentSettings = settings;
                o.SerializerSettings = serializerSettings;
                o.EnableJWTBearerAuth = addJWTBearerAuth;
                o.AutoTagPathSegmentIndex = tagIndex;
                o.TagCase = tagCase;
                o.MinEndpointVersion = minEndpointVersion;
                o.MaxEndpointVersion = maxEndpointVersion;
                o.ShortSchemaNames = shortSchemaNames;
                o.RemoveEmptyRequestSchema = removeEmptySchemas;
                o.ExcludeNonFastEndpoints = excludeNonFastEndpoints;
            });
    }

    //todo: remove at next major version
    [Obsolete("Use the TagDescriptions property on the DocumentOptions object at the top level!")]
    public static void TagDescriptions(this AspNetCoreOpenApiDocumentGeneratorSettings settings, params (string tagName, string tagDescription)[] documentTags)
    {
        settings.AddOperationFilter(ctx =>
        {
            foreach (var (tagName, tagDescription) in documentTags)
            {
                ctx.Document.Tags.Add(new OpenApiTag
                {
                    Name = tagName,
                    Description = tagDescription
                });
            }
            return true;
        });
    }
}
