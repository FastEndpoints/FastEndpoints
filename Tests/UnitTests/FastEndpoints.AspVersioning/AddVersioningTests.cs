using Asp.Versioning;
using Asp.Versioning.OpenApi;
using FastEndpoints.AspVersioning;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Unit.FastEndpoints.AspVersioning;

public class AddVersioningTests
{
    [Fact(DisplayName = "AddVersioning wires the open api options action to the versioned open api options")]
    public void AddVersioning_WithOpenApiOptions_ConfiguresVersionedOpenApi()
    {
        var services = new ServiceCollection();

        services.AddVersioning(openApiOptions: o => o.Document.AddDocumentTransformer(
            (document, _, _) =>
            {
                document.Info.Title = "My API";
                return Task.CompletedTask;
            }));

        services.Any(d => d.ServiceType == typeof(IConfigureOptions<VersionedOpenApiOptions>)).ShouldBeTrue();
    }

    [Fact(DisplayName = "AddVersioning does not register versioned OpenAPI services without open api options")]
    public void AddVersioning_WithoutOpenApiOptions_DoesNotRegisterVersionedOpenApi()
    {
        var services = new ServiceCollection();

        services.AddVersioning();

        services.Any(d => d.ServiceType == typeof(IConfigureOptions<VersionedOpenApiOptions>)).ShouldBeFalse();
        HasDocumentProvider(services).ShouldBeFalse();
        HasOpenApiPostConfigure(services).ShouldBeFalse();
    }

    [Fact(DisplayName = "AddVersioning registers the versioned OpenAPI document services")]
    public void AddVersioning_WithOpenApiOptions_RegistersVersionedOpenApi()
    {
        var services = new ServiceCollection();

        services.AddVersioning(
            o => o.ApiVersionReader = ApiVersionReader.Combine(new UrlSegmentApiVersionReader()),
            o => o.GroupNameFormat = "'v'VVV",
            o => o.Document.AddDocumentTransformer(
                (document, _, _) =>
                {
                    document.Info.Title = "My API";
                    return Task.CompletedTask;
                }));

        HasDocumentProvider(services).ShouldBeTrue();
        HasOpenApiPostConfigure(services).ShouldBeTrue();
    }

    static bool HasDocumentProvider(IServiceCollection services)
        => services.Any(d => d.ServiceType.FullName == "Microsoft.Extensions.ApiDescriptions.IDocumentProvider");

    static bool HasOpenApiPostConfigure(IServiceCollection services)
        => services.Any(d => d.ServiceType == typeof(IPostConfigureOptions<OpenApiOptions>));
}
