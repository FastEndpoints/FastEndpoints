using Microsoft.OpenApi;

static class DocumentRegistration
{
    public static IServiceCollection AddOpenApiDocuments(this IServiceCollection services)
    {
        Func<EndpointDefinition, bool> excludeReleaseVersioning = ep => ep.EndpointTags?.Contains("release_versioning") is not true;
        Func<EndpointDefinition, bool> includeReleaseVersioning = ep => ep.EndpointTags?.Contains("release_versioning") is true;
        Func<EndpointDefinition, bool> includeSwaggerReview = ep => ep.EndpointTags?.Contains("swagger_review") is true;
        Func<EndpointDefinition, bool> includeNullableOneOfRepro = ep => ep.EndpointTags?.Contains("nullable_oneOf_repro") is true;

        FastEndpoints.OpenApi.Extensions.OpenApiDocument(
            services,
            o =>
            {
                o.EndpointFilter = excludeReleaseVersioning;
                o.DocumentName = "Initial Release";
                o.Title = "Web API";
                o.Version = "v0.0";
                o.TagCase = FastEndpoints.OpenApi.TagCase.TitleCase;
                o.TagStripSymbols = true;
            });
        FastEndpoints.OpenApi.Extensions.OpenApiDocument(
            services,
            o =>
            {
                o.EndpointFilter = excludeReleaseVersioning;
                o.DocumentName = "Release 1.0";
                o.Title = "Web API";
                o.Version = "v1.0";
                o.AddAuth(
                    "ApiKey",
                    new()
                    {
                        Name = "api_key",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.ApiKey
                    });
                o.MaxEndpointVersion = 1;
                o.TagStripSymbols = true;
            });
        FastEndpoints.OpenApi.Extensions.OpenApiDocument(
            services,
            o =>
            {
                o.EndpointFilter = excludeReleaseVersioning;
                o.DocumentName = "Release 2.0";
                o.Title = "FastEndpoints Sandbox";
                o.Version = "v2.0";
                o.MaxEndpointVersion = 2;
                o.ShowDeprecatedOps = true;
                o.TagStripSymbols = true;
            });
        FastEndpoints.OpenApi.Extensions.OpenApiDocument(
            services,
            o => //only ver3 & only FastEndpoints
            {
                o.EndpointFilter = excludeReleaseVersioning;
                o.DocumentName = "Release 3.0";
                o.Title = "FastEndpoints Sandbox ver3 only";
                o.Version = "v3.0";
                o.MinEndpointVersion = 3;
                o.MaxEndpointVersion = 3;
                o.ExcludeNonFastEndpoints = true;
            });

        //used for release versioning tests
        FastEndpoints.OpenApi.Extensions.OpenApiDocument(
            services,
            o =>
            {
                o.ExcludeNonFastEndpoints = true;
                o.EndpointFilter = includeReleaseVersioning;
                o.Title = "Web API";
                o.DocumentName = "ReleaseVersioning - v0";
                o.ReleaseVersion = 0;
                o.ShowDeprecatedOps = true;
            });
        FastEndpoints.OpenApi.Extensions.OpenApiDocument(
            services,
            o =>
            {
                o.ExcludeNonFastEndpoints = true;
                o.EndpointFilter = includeReleaseVersioning;
                o.Title = "Web API";
                o.DocumentName = "ReleaseVersioning - v1";
                o.ReleaseVersion = 1;
                o.ShowDeprecatedOps = true;
            });
        FastEndpoints.OpenApi.Extensions.OpenApiDocument(
            services,
            o =>
            {
                o.ExcludeNonFastEndpoints = true;
                o.EndpointFilter = includeReleaseVersioning;
                o.Title = "Web API";
                o.DocumentName = "ReleaseVersioning - v2";
                o.ReleaseVersion = 2;
                o.ShowDeprecatedOps = true;
            });
        FastEndpoints.OpenApi.Extensions.OpenApiDocument(
            services,
            o =>
            {
                o.ExcludeNonFastEndpoints = true;
                o.EndpointFilter = includeReleaseVersioning;
                o.Title = "Web API";
                o.DocumentName = "ReleaseVersioning - v3";
                o.ReleaseVersion = 3;
                o.ShowDeprecatedOps = true;
            });
        FastEndpoints.OpenApi.Extensions.OpenApiDocument(
            services,
            o =>
            {
                o.ExcludeNonFastEndpoints = true;
                o.EndpointFilter = includeSwaggerReview;
                o.Title = "Web API";
                o.DocumentName = "Swagger Review";
                o.TagStripSymbols = true;
            });
        FastEndpoints.OpenApi.Extensions.OpenApiDocument(
            services,
            o =>
            {
                o.ExcludeNonFastEndpoints = true;
                o.EndpointFilter = includeSwaggerReview;
                o.Title = "Web API";
                o.DocumentName = "Swagger Review Empty Schema";
                o.TagStripSymbols = true;
            });
        FastEndpoints.OpenApi.Extensions.OpenApiDocument(
            services,
            o =>
            {
                o.ExcludeNonFastEndpoints = true;
                o.EndpointFilter = includeNullableOneOfRepro;
                o.Title = "Web API";
                o.DocumentName = "Nullable OneOf Repro";
                o.TagStripSymbols = true;
            });

        return services;
    }

    // public static IServiceCollection AddSwaggerDocuments(this IServiceCollection services)
    // {
    //     Func<EndpointDefinition, bool> excludeReleaseVersioning = ep => ep.EndpointTags?.Contains("release_versioning") is not true;
    //     Func<EndpointDefinition, bool> includeReleaseVersioning = ep => ep.EndpointTags?.Contains("release_versioning") is true;
    //     Func<EndpointDefinition, bool> includeSwaggerReview = ep => ep.EndpointTags?.Contains("swagger_review") is true;
    //
    //     FastEndpoints.Swagger.Extensions.SwaggerDocument(
    //         services,
    //         o =>
    //         {
    //             o.EndpointFilter = excludeReleaseVersioning;
    //             ConfigureSwaggerDocument(o, "Initial Release", "Web API", "v0.0");
    //             o.TagCase = FastEndpoints.Swagger.TagCase.TitleCase;
    //             o.TagStripSymbols = true;
    //         });
    //     FastEndpoints.Swagger.Extensions.SwaggerDocument(
    //         services,
    //         o =>
    //         {
    //             o.EndpointFilter = excludeReleaseVersioning;
    //             ConfigureSwaggerDocument(
    //                 o,
    //                 "Release 1.0",
    //                 "Web API",
    //                 "v1.0",
    //                 s => FastEndpoints.Swagger.Extensions.AddAuth(
    //                     s,
    //                     "ApiKey",
    //                     new()
    //                     {
    //                         Name = "api_key",
    //                         In = NSwag.OpenApiSecurityApiKeyLocation.Header,
    //                         Type = NSwag.OpenApiSecuritySchemeType.ApiKey
    //                     }));
    //             o.MaxEndpointVersion = 1;
    //             o.TagStripSymbols = true;
    //         });
    //     FastEndpoints.Swagger.Extensions.SwaggerDocument(
    //         services,
    //         o =>
    //         {
    //             o.EndpointFilter = excludeReleaseVersioning;
    //             ConfigureSwaggerDocument(o, "Release 2.0", "FastEndpoints Sandbox", "v2.0");
    //             o.MaxEndpointVersion = 2;
    //             o.ShowDeprecatedOps = true;
    //             o.TagStripSymbols = true;
    //         });
    //     FastEndpoints.Swagger.Extensions.SwaggerDocument(
    //         services,
    //         o => //only ver3 & only FastEndpoints
    //         {
    //             o.EndpointFilter = excludeReleaseVersioning;
    //             ConfigureSwaggerDocument(o, "Release 3.0", "FastEndpoints Sandbox ver3 only", "v3.0");
    //             o.MinEndpointVersion = 3;
    //             o.MaxEndpointVersion = 3;
    //             o.ExcludeNonFastEndpoints = true;
    //         });
    //
    //     //used for release versioning tests
    //     FastEndpoints.Swagger.Extensions.SwaggerDocument(
    //         services,
    //         o =>
    //         {
    //             o.ExcludeNonFastEndpoints = true;
    //             o.EndpointFilter = includeReleaseVersioning;
    //             ConfigureSwaggerDocument(o, "ReleaseVersioning - v0", "Web API");
    //             o.ReleaseVersion = 0;
    //             o.ShowDeprecatedOps = true;
    //         });
    //     FastEndpoints.Swagger.Extensions.SwaggerDocument(
    //         services,
    //         o =>
    //         {
    //             o.ExcludeNonFastEndpoints = true;
    //             o.EndpointFilter = includeReleaseVersioning;
    //             ConfigureSwaggerDocument(o, "ReleaseVersioning - v1", "Web API");
    //             o.ReleaseVersion = 1;
    //             o.ShowDeprecatedOps = true;
    //         });
    //     FastEndpoints.Swagger.Extensions.SwaggerDocument(
    //         services,
    //         o =>
    //         {
    //             o.ExcludeNonFastEndpoints = true;
    //             o.EndpointFilter = includeReleaseVersioning;
    //             ConfigureSwaggerDocument(o, "ReleaseVersioning - v2", "Web API");
    //             o.ReleaseVersion = 2;
    //             o.ShowDeprecatedOps = true;
    //         });
    //     FastEndpoints.Swagger.Extensions.SwaggerDocument(
    //         services,
    //         o =>
    //         {
    //             o.ExcludeNonFastEndpoints = true;
    //             o.EndpointFilter = includeReleaseVersioning;
    //             ConfigureSwaggerDocument(o, "ReleaseVersioning - v3", "Web API");
    //             o.ReleaseVersion = 3;
    //             o.ShowDeprecatedOps = true;
    //         });
    //     FastEndpoints.Swagger.Extensions.SwaggerDocument(
    //         services,
    //         o =>
    //         {
    //             o.ExcludeNonFastEndpoints = true;
    //             o.EndpointFilter = includeSwaggerReview;
    //             ConfigureSwaggerDocument(o, "Swagger Review", "Web API");
    //             o.TagStripSymbols = true;
    //         });
    //     FastEndpoints.Swagger.Extensions.SwaggerDocument(
    //         services,
    //         o =>
    //         {
    //             o.ExcludeNonFastEndpoints = true;
    //             o.EndpointFilter = includeSwaggerReview;
    //             ConfigureSwaggerDocument(o, "Swagger Review Empty Schema", "Web API");
    //             o.RemoveEmptyRequestSchema = true;
    //             o.TagStripSymbols = true;
    //         });
    //
    //     return services;
    //
    //     static void ConfigureSwaggerDocument(FastEndpoints.Swagger.DocumentOptions options,
    //                                          string documentName,
    //                                          string title,
    //                                          string? version = null,
    //                                          Action<NSwag.Generation.AspNetCore.AspNetCoreOpenApiDocumentGeneratorSettings>? configure = null)
    //     {
    //         options.DocumentSettings = s =>
    //                                    {
    //                                        s.DocumentName = documentName;
    //                                        s.Title = title;
    //
    //                                        if (version is not null)
    //                                            s.Version = version;
    //
    //                                        s.SchemaSettings.SchemaType = NJsonSchema.SchemaType.OpenApi3;
    //                                        configure?.Invoke(s);
    //                                    };
    //     }
    // }
}