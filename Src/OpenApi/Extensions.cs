using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

/// <summary>
/// a set of extension methods for adding swagger support via Microsoft.AspNetCore.OpenApi
/// </summary>
public static class Extensions
{
    /// <summary>
    /// JsonNamingPolicy chosen for swagger
    /// </summary>
    public static JsonNamingPolicy? SelectedJsonNamingPolicy { get; private set; }

    /// <summary>
    /// resolved naming policy: the swagger-selected policy, falling back to the serializer options policy.
    /// use this instead of repeating <c>SelectedJsonNamingPolicy ?? Cfg.SerOpts.Options.PropertyNamingPolicy</c>.
    /// </summary>
    internal static JsonNamingPolicy? NamingPolicy => SelectedJsonNamingPolicy ?? Cfg.SerOpts.Options.PropertyNamingPolicy;

    /// <summary>
    /// enable support for FastEndpoints and create an open-api document.
    /// </summary>
    /// <param name="services">the service collection</param>
    /// <param name="options">swagger document configuration options</param>
    public static IServiceCollection OpenApiDocument(this IServiceCollection services, Action<DocumentOptions>? options = null)
    {
        var opts = new DocumentOptions();
        options?.Invoke(opts);

        var docSettings = new DocumentSettings();

        // add JWT bearer auth if enabled (before user-defined schemes so it appears first)
        if (opts.EnableJWTBearerAuth)
        {
            docSettings.AuthSchemes.Add(
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

        opts.DocumentSettings?.Invoke(docSettings);

        var stjOpts = new JsonSerializerOptions(Cfg.SerOpts.Options);
        SelectedJsonNamingPolicy = stjOpts.PropertyNamingPolicy;

        var sharedCtx = new SharedContext();

        services.AddOpenApi(
            docSettings.DocumentName,
            apiOptions =>
            {
                // schema naming
                apiOptions.CreateSchemaReferenceId = SchemaNameGenerator.Create(opts.ShortSchemaNames);

                // add transformers
                apiOptions.AddOperationTransformer(new OperationTransformer(opts, docSettings, sharedCtx));
                apiOptions.AddDocumentTransformer(new DocumentTransformer(opts, docSettings, sharedCtx));
                apiOptions.AddSchemaTransformer<ValidationSchemaTransformer>();
                apiOptions.AddSchemaTransformer<XmlDocSchemaTransformer>();
                apiOptions.AddSchemaTransformer<NumericTypeCleanupSchemaTransformer>();
                apiOptions.AddSchemaTransformer<ToHeaderPropertySchemaTransformer>();
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

    internal static string Remove(this string value, string removeString)
    {
        var index = value.IndexOf(removeString, StringComparison.Ordinal);

        return index < 0 ? value : value.Remove(index, removeString.Length);
    }
}
