using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

/// <summary>
/// a set of extension methods for adding swagger support via Microsoft.AspNetCore.OpenApi
/// </summary>
public static class Extensions
{
    /// <summary>
    /// enable support for FastEndpoints and create an open-api document.
    /// </summary>
    /// <param name="services">the service collection</param>
    /// <param name="options">swagger document configuration options</param>
    public static IServiceCollection OpenApiDocument(this IServiceCollection services, Action<DocumentOptions>? options = null)
    {
        var opts = new DocumentOptions();
        options?.Invoke(opts);

        // add JWT bearer auth if enabled (before user-defined schemes so it appears first)
        if (opts.EnableJWTBearerAuth)
        {
            opts.AuthSchemes.Insert(
                0,
                new(
                    "JWTBearerAuth",
                    new()
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "Bearer",
                        BearerFormat = "JWT",
                        Description = "Enter a JWT token to authorize the requests..."
                    },
                    null));
        }

        var sharedCtx = new SharedContext();

        services.AddOpenApi(
            opts.DocumentName,
            apiOptions =>
            {
                // schema naming
                apiOptions.CreateSchemaReferenceId = SchemaNameGenerator.Create(opts.ShortSchemaNames);

                // add transformers
                apiOptions.AddOperationTransformer(new OperationTransformer(opts, sharedCtx));
                apiOptions.AddDocumentTransformer(new DocumentTransformer(opts, sharedCtx));
                apiOptions.AddSchemaTransformer<XmlDocSchemaTransformer>();
                apiOptions.AddSchemaTransformer<NumericTypeCleanupSchemaTransformer>();
                apiOptions.AddSchemaTransformer(new ToHeaderPropertySchemaTransformer(opts, sharedCtx));
                apiOptions.AddSchemaTransformer<EnumSchemaTransformer>();

                if (opts.UseOneOfForPolymorphism)
                    apiOptions.AddSchemaTransformer(new PolymorphismSchemaTransformer(opts));

                // apply user's advanced configuration
                opts.ConfigureOpenApi?.Invoke(apiOptions);
            });

        return services;
    }

    /// <summary>
    /// when path based auto-tagging is enabled, you can use this method to specify an override tag name if necessary.
    /// </summary>
    /// <param name="b">the endpoint convention builder</param>
    /// <param name="tag">the tag name to use instead of the auto tag</param>
    public static IEndpointConventionBuilder AutoTagOverride(this IEndpointConventionBuilder b, string tag)
    {
        b.WithMetadata(new AutoTagOverride(tag));

        return b;
    }

    /// <summary>
    /// disable swagger+fluentvalidation integration for a property rule
    /// </summary>
    public static IRuleBuilderOptions<T, TProperty> SwaggerIgnore<T, TProperty>(this IRuleBuilderOptions<T, TProperty> rule,
                                                                                ApplyConditionTo applyConditionTo = ApplyConditionTo.AllValidators)
    {
        return rule.When(_ => true, applyConditionTo);
    }

    const string OpenApiJsonExportKey = "export-openapi-docs";

    extension(IHost app)
    {
        /// <summary>
        /// returns true if the app is being launched just to export openapi json files.
        /// </summary>
        public bool IsJsonExportMode()
            => string.Equals(app.Services.GetRequiredService<IConfiguration>()[OpenApiJsonExportKey], "true", StringComparison.Ordinal);

        /// <summary>
        /// returns true if the app is running normally and not launched for the purpose of exporting openapi json files.
        /// </summary>
        public bool IsNotJsonExportMode()
            => !app.IsJsonExportMode();
    }

    extension(IHostApplicationBuilder bld)
    {
        /// <summary>
        /// returns true if the app is being launched just to export openapi json files.
        /// </summary>
        public bool IsJsonExportMode()
            => string.Equals(bld.Configuration[OpenApiJsonExportKey], "true", StringComparison.Ordinal);

        /// <summary>
        /// returns true if the app is running normally and not launched for the purpose of exporting openapi json files.
        /// </summary>
        public bool IsNotJsonExportMode()
            => !bld.IsJsonExportMode();
    }

    /// <summary>
    /// exports openapi .json files to disk and exits the program.
    /// <para>HINT: make sure to place the call straight after <c>app.UseFastEndpoints()</c></para>
    /// <para>
    /// to enable automatic export during AOT publish builds, add this to your .csproj:
    /// <code>
    /// &lt;PropertyGroup&gt;
    ///     &lt;ExportOpenApiDocs&gt;true&lt;/ExportOpenApiDocs&gt;
    /// &lt;/PropertyGroup&gt;
    /// </code>
    /// </para>
    /// <para>
    /// to customize the export path, add this to your .csproj:
    /// <code>
    /// &lt;PropertyGroup&gt;
    ///     &lt;OpenApiExportPath&gt;wwwroot/openapi&lt;/OpenApiExportPath&gt;
    /// &lt;/PropertyGroup&gt;
    /// </code>
    /// </para>
    /// <para>
    /// to force generate openapi docs outside a AOT publish, run the following in a terminal:
    /// <code>dotnet run --export-openapi-docs true -p:PublishAot=false</code>
    /// optionally specify the output folder:
    /// <code>dotnet run --export-openapi-docs true -p:PublishAot=false -p:OpenApiExportPath=wwwroot/openapi</code>
    /// </para>
    /// </summary>
    /// <param name="documentNames">the openapi document names to export. these must match the names used in <c>.OpenApiDocument()</c> configuration.</param>
    public static async Task ExportOpenApiDocsAndExitAsync(this WebApplication app, params string[] documentNames)
    {
        if (app.IsNotJsonExportMode())
            return;

        if (documentNames.Length == 0)
            return;

        var destinationPath = Path.Combine(app.Environment.ContentRootPath, DocumentOptions.OpenApiExportPath);
        var logger = app.Services.GetRequiredService<ILogger<OpenApiExportRunner>>();

        await app.StartAsync();
        var exportFailed = false;

        try
        {
            Directory.CreateDirectory(destinationPath);

            foreach (var docName in documentNames)
            {
                try
                {
                    logger.ExportingOpenApiDoc(docName);
                    var filePath = await ExportOpenApiDocument(app, docName, destinationPath, CancellationToken.None);
                    logger.OpenApiDocExportSuccessful(docName, filePath);
                }
                catch (Exception ex)
                {
                    exportFailed = true;
                    logger.OpenApiDocExportFailed(ex, docName);
                }
            }
        }
        finally
        {
            await app.StopAsync();
        }

        Environment.Exit(exportFailed ? 1 : 0);
    }

    static async Task<string> ExportOpenApiDocument(WebApplication app, string documentName, string destinationPath, CancellationToken ct)
    {
        var provider = app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(documentName);
        var openApiVersion = app.Services.GetRequiredService<IOptionsMonitor<OpenApiOptions>>().Get(documentName).OpenApiVersion;
        var doc = await provider.GetOpenApiDocumentAsync(ct);
        var json = await doc.SerializeAsJsonAsync(openApiVersion, ct);
        var filePath = Path.Combine(destinationPath, $"{documentName}.json");

        await File.WriteAllTextAsync(filePath, json, ct);

        return filePath;
    }
}