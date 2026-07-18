using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
                apiOptions.CreateSchemaReferenceId = SchemaNameGenerator.Create(opts.ShortSchemaNames, sharedCtx.SchemaNames);

                // add transformers
                apiOptions.AddOperationTransformer(new OperationTransformer(opts, sharedCtx));
                apiOptions.AddDocumentTransformer(new DocumentTransformer(opts, sharedCtx));
                apiOptions.AddSchemaTransformer<XmlDocSchemaTransformer>();
                apiOptions.AddSchemaTransformer<NumericTypeCleanupSchemaTransformer>();
                apiOptions.AddSchemaTransformer<UniqueItemsSchemaTransformer>();
                apiOptions.AddSchemaTransformer(new HiddenPropertySchemaTransformer(opts, sharedCtx));
                apiOptions.AddSchemaTransformer(new ToHeaderPropertySchemaTransformer(opts, sharedCtx));
                apiOptions.AddSchemaTransformer(new EnumSchemaTransformer(sharedCtx));

                if (opts.UseOneOfForPolymorphism)
                    apiOptions.AddSchemaTransformer(new PolymorphismSchemaTransformer(opts));

                // apply user's advanced configuration
                opts.ConfigureOpenApi?.Invoke(apiOptions);
            });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<JsonOptions>, FeOpenApiJsonOptions>());

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
    const string OpenApiHttpExportKey = "export-http-files";

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

        /// <summary>
        /// returns true if the app is being launched just to export '.http' files.
        /// </summary>
        public bool IsHttpExportMode()
            => string.Equals(app.Services.GetRequiredService<IConfiguration>()[OpenApiHttpExportKey], "true", StringComparison.Ordinal);

        /// <summary>
        /// returns true if the app is running normally and not launched for the purpose of exporting '.http' files.
        /// </summary>
        public bool IsNotHttpExportMode()
            => !app.IsHttpExportMode();
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

        /// <summary>
        /// returns true if the app is being launched just to export '.http' files.
        /// </summary>
        public bool IsHttpExportMode()
            => string.Equals(bld.Configuration[OpenApiHttpExportKey], "true", StringComparison.Ordinal);

        /// <summary>
        /// returns true if the app is running normally and not launched for the purpose of exporting '.http' files.
        /// </summary>
        public bool IsNotHttpExportMode()
            => !bld.IsHttpExportMode();
    }

    /// <summary>
    /// exports openapi .json files to disk and exits the program.
    /// <para>HINT: make sure to place the call straight after <c>app.UseFastEndpoints()</c></para>
    /// <para>
    /// when both <c>--export-openapi-docs</c> and <c>--export-http-files</c> are set, call
    /// <see cref="ExportOpenApiDocsAndExitAsync"/> and <see cref="ExportHttpFilesAndExitAsync"/> in sequence;
    /// the process starts once and exits after the last requested format finishes.
    /// </para>
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
    /// to force generate openapi docs outside an AOT publish, run the following in a terminal:
    /// <code>dotnet run --export-openapi-docs true -p:PublishAot=false</code>
    /// optionally specify the output folder:
    /// <code>dotnet run --export-openapi-docs true -p:PublishAot=false -p:OpenApiExportPath=wwwroot/openapi</code>
    /// </para>
    /// </summary>
    /// <param name="documentNames">the openapi document names to export. these must match the names used in <c>.OpenApiDocument()</c> configuration.</param>
    public static Task ExportOpenApiDocsAndExitAsync(this WebApplication app, params string[] documentNames)
        => OpenApiExporter.ExportDocsAndExitAsync(app, documentNames);

    /// <summary>
    /// exports '.http' files (REST Client / HTTP Client format) to disk and exits the program.
    /// <para>HINT: make sure to place the call straight after <c>app.UseFastEndpoints()</c></para>
    /// <para>
    /// when both <c>--export-openapi-docs</c> and <c>--export-http-files</c> are set, call
    /// <see cref="ExportOpenApiDocsAndExitAsync"/> and <see cref="ExportHttpFilesAndExitAsync"/> in sequence;
    /// the process starts once and exits after the last requested format finishes.
    /// </para>
    /// <para>
    /// to enable automatic export during AOT publish builds, add this to your .csproj:
    /// <code>
    /// &lt;PropertyGroup&gt;
    ///     &lt;ExportHttpFiles&gt;true&lt;/ExportHttpFiles&gt;
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
    /// to force generate '.http' files outside an AOT publish, run the following in a terminal:
    /// <code>dotnet run --export-http-files true -p:PublishAot=false</code>
    /// optionally specify the output folder:
    /// <code>dotnet run --export-http-files true -p:PublishAot=false -p:OpenApiExportPath=wwwroot/openapi</code>
    /// </para>
    /// </summary>
    /// <param name="documentNames">the openapi document names to export. these must match the names used in <c>.OpenApiDocument()</c> configuration.</param>
    public static Task ExportHttpFilesAndExitAsync(this WebApplication app, params string[] documentNames)
        => OpenApiExporter.ExportHttpFilesAndExitAsync(app, documentNames);
}