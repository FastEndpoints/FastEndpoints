using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Namotion.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using NSwag;
using NSwag.AspNetCore;
using NSwag.Generation;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors.Contexts;
using NSwag.Generation.Processors.Security;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

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
        if (!RuntimeFeature.IsDynamicCodeSupported)
            return services;

        services.AddEndpointsApiExplorer();
        services.AddOpenApiDocument(
            (genSettings, serviceProvider) =>
            {
                var doc = new DocumentOptions(serviceProvider);
                options?.Invoke(doc);

                var stjOpts = new JsonSerializerOptions(Cfg.SerOpts.Options);
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
                    genSettings.PostProcess +=
                        d =>
                        {
                            foreach (var kvp in dict)
                            {
                                d.Tags.Add(
                                    new()
                                    {
                                        Name = kvp.Key,
                                        Description = kvp.Value
                                    });
                            }
                        };
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
        if (!RuntimeFeature.IsDynamicCodeSupported)
            throw new NotSupportedException("Not supported in AOT applications! Use Scalar for API visualization.");

        app.UseOpenApi(config);
        app.UseSwaggerUi((c => c.ConfigureDefaults()) + uiConfig);

        return app;
    }

    /// <summary>
    /// enable support for FastEndpoints in swagger
    /// </summary>
    /// <param name="documentOptions">the document options</param>
    /// <param name="serviceProvider">the service provider</param>
    public static void EnableFastEndpoints(this AspNetCoreOpenApiDocumentGeneratorSettings settings,
                                           Action<DocumentOptions> documentOptions,
                                           IServiceProvider serviceProvider)
    {
        var doc = new DocumentOptions(serviceProvider);
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
        s.CustomHeadContent = """
                              <script>
                              const SearchPlugin = (system) => ({
                                fn: {
                                  opsFilter: (taggedOps, phrase) => {
                                    const words = phrase.toLowerCase().split(/\s+/);
                                    const allOps = JSON.parse(JSON.stringify(taggedOps));
                                    for (const tagObj in allOps) {
                                      let ops = allOps[tagObj].operations;
                                      ops = ops.filter(op => {
                                        const toLowerSafe = (value) => (value ? value.toLowerCase() : '');
                                        const searchProps = [
                                          toLowerSafe(op.path),
                                          toLowerSafe(op.operation.summary),
                                          toLowerSafe(op.operation.description),
                                          toLowerSafe(JSON.stringify(op.operation.parameters)),
                                          toLowerSafe(JSON.stringify(op.operation.responses)),
                                          toLowerSafe(JSON.stringify(op.operation.requestBody))
                                        ];
                                        return words.every(word => searchProps.some(prop => prop.includes(word)));
                                      });
                                      if (ops.length) {
                                        allOps[tagObj].operations = ops;
                                      } else {
                                        delete allOps[tagObj];
                                      }
                                    }
                                    return system.Im.fromJS(allOps);
                                  }
                                }
                              });

                              const initPlugin = () => {
                                const ui = window.ui || window.swaggerUi;
                                if (ui && ui.getSystem) {
                                  ui.getSystem().fn.opsFilter = SearchPlugin(ui.getSystem()).fn.opsFilter;
                                  const searchBox = document.querySelector('.filter-container input');
                                  if (searchBox) {
                                    searchBox.placeholder = "Search...";
                                    searchBox.addEventListener('input', (event) => {
                                      const phrase = event.target.value;
                                      const system = ui.getSystem();
                                      const taggedOps = system.getState().toJS().taggedOps;
                                      const filteredOps = system.fn.opsFilter(taggedOps, phrase);
                                      system.getState().update('taggedOps', () => filteredOps);
                                    });
                                  }
                                } else {
                                  setTimeout(initPlugin, 250);
                                }
                              };
                              document.addEventListener('DOMContentLoaded', initPlugin);
                              </script>
                              """;
        settings?.Invoke(s);
    }

    /// <summary>
    /// the "Try It Out" button is activated by default. call this method to de-activate it by default.
    /// set <see cref="SwaggerUiSettings.EnableTryItOut" /> to <c>false</c> to remove the button from ui.
    /// </summary>
    public static void DeActivateTryItOut(this SwaggerUiSettings s)
        => s.AdditionalSettings.Remove("tryItOutEnabled");

    /// <summary>
    /// displays the swagger operation id in the swagger ui
    /// </summary>
    public static void ShowOperationIDs(this SwaggerUiSettings s)
        => s.AdditionalSettings["displayOperationId"] = true;

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
    /// gets the <see cref="EndpointDefinition" /> from the nswag operation processor context if this is a FastEndpoint operation. otherwise returns null.
    /// </summary>
    public static EndpointDefinition? GetEndpointDefinition(this OperationProcessorContext ctx)
        => ((AspNetCoreOperationProcessorContext)ctx)
           .ApiDescription
           .ActionDescriptor
           .EndpointMetadata
           .OfType<EndpointDefinition>()
           .SingleOrDefault();

    /// <summary>
    /// gets the example object if any, from a given <see cref="DefaultProducesResponseMetadata" /> internal class
    /// </summary>
    public static object? GetExampleFromMetaData(this IProducesResponseTypeMetadata metadata)
        => (metadata as DefaultProducesResponseMetadata)?.Example;

    /// <summary>
    /// when path based auto-tagging is enabled, you can use this method to specify an override tag name if necessary.
    /// </summary>
    /// <param name="tag">the tag name to use instead of the auto tag</param>
    public static IEndpointConventionBuilder AutoTagOverride(this IEndpointConventionBuilder b, string tag)
    {
        b.WithMetadata(new AutoTagOverride(tag));

        return b;
    }

    /// <summary>
    /// disable swagger+fluentvalidation integration for a property rule
    /// </summary>
    /// <param name="applyConditionTo"></param>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TProperty"></typeparam>
    public static IRuleBuilderOptions<T, TProperty> SwaggerIgnore<T, TProperty>(this IRuleBuilderOptions<T, TProperty> rule,
                                                                                ApplyConditionTo applyConditionTo = ApplyConditionTo.AllValidators)
    {
        return rule.When(_ => true, applyConditionTo);
    }

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
               .Value.Schema.ActualSchema.ActualProperties.Union(
                   mediaType
                       .Value.Schema.ActualSchema.AllInheritedSchemas
                       .Select(s => s.ActualProperties)
                       .SelectMany(s => s.Select(s => s)));
    }

    internal static IEnumerable<KeyValuePair<string, JsonSchemaProperty>> GetAllProperties(this KeyValuePair<string, OpenApiResponse> response)
    {
        return response
               .Value.Schema.ActualSchema.ActualProperties.Union(
                   response.Value.Schema.ActualSchema.AllInheritedSchemas
                           .Select(s => s.ActualProperties)
                           .SelectMany(s => s.Select(s => s)));
    }

    internal static object? GetParentCtorDefaultValue(this PropertyInfo p)
    {
        var tParent = p.DeclaringType;

        if (tParent?.IsClass is not true)
            return null;

        return tParent.GetConstructors()
                      .Select(c => c.GetParameters())
                      .MaxBy(pi => pi.Length)?
                      .SingleOrDefault(
                          pi => pi.HasDefaultValue &&
                                pi.Name?.Equals(p.Name, StringComparison.OrdinalIgnoreCase) is true)?.DefaultValue;
    }

    static readonly ConcurrentDictionary<PropertyInfo, NullabilityInfo> _nullInfoCache = new();

    internal static bool IsNullable(this PropertyInfo prop)
    {
        return _nullInfoCache.GetOrAdd(prop, pi => new NullabilityInfoContext().Create(pi))
                             .WriteState is NullabilityState.Nullable;
    }

    internal static string? GetXmlExample(this PropertyInfo p)
    {
        var example = p.GetXmlDocsTag("example");

        return string.IsNullOrEmpty(example) ? null : example;
    }

    internal static JToken? GetExampleJToken(this PropertyInfo? p, JsonSerializer serializer)
    {
        var exampleStr = p?.GetXmlExample();

        if (exampleStr is null)
            return null;

        if (!exampleStr.IsJsonObjectString() && !exampleStr.IsJsonArrayString())
            return exampleStr;

        try
        {
            return JToken.FromObject(JsonConvert.DeserializeObject(exampleStr)!, serializer);
        }
        catch
        {
            return null;
        }
    }

    static bool IsJsonArrayString(this string? val)
        => val?.Length > 1 && val[0] == '[' && val[^1] == ']';

    static bool IsJsonObjectString(this string? val)
        => val?.Length > 1 && val[0] == '{' && val[^1] == '}';

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
        => documentOptions.UsePropertyNamingPolicy && SelectedJsonNamingPolicy is not null
               ? SelectedJsonNamingPolicy.ConvertName(paramName)
               : paramName;

    internal static bool IsSwagger2(this OperationProcessorContext ctx)
        => ctx.Settings.SchemaSettings.SchemaType == SchemaType.Swagger2;

    static void EnableFastEndpoints(AspNetCoreOpenApiDocumentGeneratorSettings settings, DocumentOptions opts)
    {
        var validationProcessor = (ValidationSchemaProcessor)opts.Services.GetRequiredService<IServiceResolver>().CreateSingleton(typeof(ValidationSchemaProcessor));

        settings.Title = AppDomain.CurrentDomain.FriendlyName;
        settings.SchemaSettings.SchemaNameGenerator = new SchemaNameGenerator(opts.ShortSchemaNames);
        settings.SchemaSettings.SchemaProcessors.Add(validationProcessor);
        settings.SchemaSettings.SchemaProcessors.Add(new PolymorphismSchemaProcessor(opts));
        settings.OperationProcessors.Add(new OperationProcessor(opts));
        settings.DocumentProcessors.Add(new DocumentProcessor(opts.MinEndpointVersion, opts.MaxEndpointVersion, opts.ReleaseVersion, opts.ShowDeprecatedOps));
    }

    internal static IEnumerable<KeyValuePair<string, JsonSchemaProperty>> GetAllRequestProperties(this KeyValuePair<string, OpenApiMediaType> mediaType)
    {
        var allProperties = mediaType.Value.Schema.ActualSchema.ActualProperties.ToList();

        foreach (var inheritedSchema in mediaType.Value.Schema.ActualSchema.AllInheritedSchemas)
            allProperties.AddRange(inheritedSchema.ActualProperties);

        var res = new List<KeyValuePair<string, JsonSchemaProperty>>();
        var visitedSchemas = new HashSet<JsonSchema>();
        const int maxDepth = 100;

        TraverseProperties(string.Empty, allProperties.DistinctBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value), res, visitedSchemas, 0, maxDepth);

        return res;

        static void TraverseProperties(string parentPath,
                                       IReadOnlyDictionary<string, JsonSchemaProperty> props,
                                       List<KeyValuePair<string, JsonSchemaProperty>> result,
                                       HashSet<JsonSchema> visitedSchemas,
                                       int currentDepth,
                                       int maxDepth)
        {
            if (currentDepth > maxDepth)
                return;

            foreach (var prop in props)
            {
                var currentPath = string.IsNullOrEmpty(parentPath)
                                      ? prop.Key
                                      : $"{parentPath}.{prop.Key}";

                result.Add(new(currentPath, prop.Value));

                if (!visitedSchemas.Add(prop.Value.ActualSchema))
                    continue;

                if (prop.Value.ActualSchema.ActualProperties.Any())
                    TraverseProperties(currentPath, prop.Value.ActualSchema.ActualProperties, result, visitedSchemas, currentDepth + 1, maxDepth);

                if (!IsCollectionType(prop.Value))
                    continue;

                var itemSchema = prop.Value.ActualSchema.Item?.ActualSchema;

                if (itemSchema == null || !itemSchema.ActualProperties.Any() || visitedSchemas.Contains(itemSchema))
                    continue;

                var collectionPath = $"{currentPath}[0]";
                visitedSchemas.Add(itemSchema);
                TraverseProperties(collectionPath, itemSchema.ActualProperties, result, visitedSchemas, currentDepth + 1, maxDepth);
            }
        }

        static bool IsCollectionType(JsonSchemaProperty property)
        {
            return property.ActualSchema.Type == JsonObjectType.Array ||
                   (property.ActualSchema.Type == JsonObjectType.Object &&
                    property.ActualSchema.IsNullable(SchemaType.OpenApi3) &&
                    property.ActualSchema.AllOf.Any(schema => schema.Type == JsonObjectType.Array));
        }
    }

    /// <summary>
    /// exports swagger.json files to disk (ONLY DURING NATIVE AOT PUBLISHING) and exits the program.
    /// <para>HINT: make sure to place the call straight after <c>app.UseFastEndpoints()</c></para>
    /// <para>
    /// to enable automatic export during AOT publish builds, add this to your .csproj:
    /// <code>
    /// &lt;PropertyGroup&gt;
    ///     &lt;ExportSwaggerDocs&gt;true&lt;/ExportSwaggerDocs&gt;
    /// &lt;/PropertyGroup&gt;
    /// </code>
    /// </para>
    /// <para>
    /// to customize the export path, add this to your .csproj:
    /// <code>
    /// &lt;PropertyGroup&gt;
    ///     &lt;SwaggerExportPath&gt;wwwroot/swagger&lt;/SwaggerExportPath&gt;
    /// &lt;/PropertyGroup&gt;
    /// </code>
    /// </para>
    /// <para>
    /// to force generate swagger docs outside a AOT publish, run the following in a terminal:
    /// <code>dotnet run --export-swagger-docs true -p:PublishAot=false</code>
    /// optionally specify the output folder:
    /// <code>dotnet run --export-swagger-docs true -p:PublishAot=false -p:SwaggerExportPath=wwwroot/swagger</code>
    /// </para>
    /// </summary>
    /// <param name="documentNames">the swagger document names to export. these must match the names used in <c>.SwaggerDocument()</c> configuration.</param>
    public static async Task ExportSwaggerDocsAndExitAsync(this WebApplication app, params string[] documentNames)
    {
        if (app.Configuration["export-swagger-docs"] != "true")
            return;

        if (documentNames.Length == 0)
            return;

        var destinationPath = Path.Combine(app.Environment.ContentRootPath, DocumentOptions.SwaggerExportPath);

        await app.StartAsync();

        var logger = app.Services.GetRequiredService<ILogger<SwaggerExportRunner>>();
        var generator = app.Services.GetRequiredService<IOpenApiDocumentGenerator>();

        Directory.CreateDirectory(destinationPath);

        foreach (var docName in documentNames)
        {
            try
            {
                logger.ExportingSwaggerDoc(docName);
                var doc = await generator.GenerateAsync(docName);
                var json = doc.ToJson();
                var filePath = Path.Combine(destinationPath, $"{docName}.json");
                await File.WriteAllTextAsync(filePath, json);
                logger.SwaggerDocExportSuccessful(docName, filePath);
            }
            catch (Exception ex)
            {
                logger.SwaggerDocExportFailed(docName, ex.Message);
            }
        }

        await app.StopAsync();
        Environment.Exit(0);
    }

    internal static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(dict);

        if (dict.TryGetValue(key, out var existing))
            return existing;

        dict[key] = value;

        return value;
    }
}