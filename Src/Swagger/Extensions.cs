using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Namotion.Reflection;
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
using static FastEndpoints.Config;

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
    /// enable support for FastEndpoints in swagger
    /// </summary>
    /// <param name="tagIndex">the index of the route path segment to use for tagging/grouping endpoints</param>
    /// <param name="tagCase">the casing strategy to use on endpoint tags</param>
    /// <param name="minEndpointVersion">endpoints lower than this vesion will not be included in the swagger doc</param>
    /// <param name="maxEndpointVersion">endpoints greater than this version will not be included in the swagger doc</param>
    /// <param name="shortSchemaNames">set to true to make schema names just the name of the class instead of full type name</param>
    /// <param name="removeEmptySchemas">
    /// set to true for removing empty schemas from the swagger document.
    /// <para>WARNING: enabling this also flattens the inheritance hierachy of the schmemas.</para>
    /// </param>
    public static void EnableFastEndpoints(this AspNetCoreOpenApiDocumentGeneratorSettings settings,
                                           int tagIndex,
                                           TagCase tagCase,
                                           int minEndpointVersion,
                                           int maxEndpointVersion,
                                           bool shortSchemaNames,
                                           bool removeEmptySchemas)
    {
        settings.Title = AppDomain.CurrentDomain.FriendlyName;
        settings.SchemaNameGenerator = new SchemaNameGenerator(shortSchemaNames);
        settings.SchemaProcessors.Add(new ValidationSchemaProcessor());
        settings.OperationProcessors.Add(new OperationProcessor(tagIndex, removeEmptySchemas, tagCase));
        settings.DocumentProcessors.Add(new DocumentProcessor(minEndpointVersion, maxEndpointVersion));
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
    /// add <see cref="OpenApiTag"/>s to a swagger document in order to provide tag descriptions
    /// </summary>
    /// <param name="documentTags">the <see cref="OpenApiTag"/>s to be added to the swagger doc</param>
    public static void TagDescriptions(this AspNetCoreOpenApiDocumentGeneratorSettings settings, params OpenApiTag[] documentTags)
    {
        settings.AddOperationFilter(ctx =>
        {
            foreach (var t in documentTags)
                ctx.Document.Tags.Add(t);
            return true;
        });
    }

    /// <summary>
    /// enable swagger support for FastEndpoints with a single call.
    /// </summary>
    /// <param name="settings">swaggergen config settings</param>
    /// <param name="serializerSettings">json serializer options</param>
    /// <param name="addJWTBearerAuth">set to false to disable auto addition of jwt bearer auth support</param>
    /// <param name="tagIndex">the index of the route path segment to use for tagging/grouping endpoints</param>
    /// <param name="tagCase">the casing strategy to use on endpoint tags</param>
    /// <param name="minEndpointVersion">endpoints lower than this vesion will not be included in the swagger doc</param>
    /// <param name="maxEndpointVersion">endpoints greater than this version will not be included in the swagger doc</param>
    /// <param name="shortSchemaNames">set to true if you'd like schema names to be the class name intead of the full name</param>
    /// <param name="removeEmptySchemas">
    /// set to true for removing empty schemas from the swagger document.
    /// <para>WARNING: enabling this also flattens the inheritance hierachy of the schmemas.</para>
    /// </param>
    /// <param name="excludeNonFastEndpoints">if set to true, only FastEndpoints will show up in the swagger doc</param>
    public static IServiceCollection AddSwaggerDoc(this IServiceCollection services,
                                                   Action<AspNetCoreOpenApiDocumentGeneratorSettings>? settings = null,
                                                   Action<JsonSerializerOptions>? serializerSettings = null,
                                                   bool addJWTBearerAuth = true,
                                                   int tagIndex = 1,
                                                   TagCase tagCase = TagCase.TitleCase,
                                                   int minEndpointVersion = 0,
                                                   int maxEndpointVersion = 0,
                                                   bool shortSchemaNames = false,
                                                   bool removeEmptySchemas = false,
                                                   bool excludeNonFastEndpoints = false)
    {
        services.AddEndpointsApiExplorer();
        services.AddOpenApiDocument(s =>
        {
            var stjOpts = new JsonSerializerOptions(SerOpts.Options);
            SelectedJsonNamingPolicy = stjOpts.PropertyNamingPolicy;
            serializerSettings?.Invoke(stjOpts);
            s.SerializerSettings = SystemTextJsonUtilities.ConvertJsonOptionsToNewtonsoftSettings(stjOpts);
            s.EnableFastEndpoints(tagIndex, tagCase, minEndpointVersion, maxEndpointVersion, shortSchemaNames, removeEmptySchemas);
            if (excludeNonFastEndpoints) s.OperationProcessors.Insert(0, new FastEndpointsFilter());
            if (addJWTBearerAuth) s.EnableJWTBearerAuth();
            settings?.Invoke(s);
            if (removeEmptySchemas) s.FlattenInheritanceHierarchy = removeEmptySchemas; //gotta force this here even if user asks not to flatten
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
    /// specify a function to filter out endpoints from the swagger document.
    /// this function will be run against every fast endpoint discovered. return true to include the endpoint and return false to exclude the endpoint from the swagger doc.
    /// </summary>
    /// <param name="filter">a function to use for filtering endpoints</param>
    public static void EndpointFilter(this AspNetCoreOpenApiDocumentGeneratorSettings x, Func<EndpointDefinition, bool> filter)
        => x.OperationProcessors.Insert(0, new EndpointFilter(filter));

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
}