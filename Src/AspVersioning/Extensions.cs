using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NSwag.Generation.AspNetCore;

namespace FastEndpoints.AspVersioning;

public static class Extensions
{
    /// <summary>
    /// add Asp.Versioning.Http versioning support to the middleware pipeline
    /// </summary>
    /// <param name="versioningOptions">action for configuring the verioning options</param>
    /// <param name="apiExplorerOptions">action for configuring the api explorer options</param>
    public static IServiceCollection AddVersioning(this IServiceCollection services,
                                                   Action<ApiVersioningOptions>? versioningOptions = null,
                                                   Action<ApiExplorerOptions>? apiExplorerOptions = null)
    {
        var builder = versioningOptions is null
                      ? services.AddApiVersioning(VersioningDefaults)
                      : services.AddApiVersioning(versioningOptions);

        var tmp = new ApiExplorerOptions();
        if (apiExplorerOptions is null)
        {
            builder.AddApiExplorer(ExplorerDefaults);
            ExplorerDefaults(tmp);
        }
        else
        {
            builder.AddApiExplorer(apiExplorerOptions);
            apiExplorerOptions(tmp);
        }
        VersionSets.VersionFormat = tmp.GroupNameFormat;

        Config.VerOpts.IsUsingAspVersioning = true;

        return services;

        static void VersioningDefaults(ApiVersioningOptions o)
        {
            o.DefaultApiVersion = new(1.0);
            o.AssumeDefaultVersionWhenUnspecified = true;
            o.ApiVersionReader = new HeaderApiVersionReader("X-Api-Version");
        }
        static void ExplorerDefaults(ApiExplorerOptions o) => o.GroupNameFormat = "'v'VVV";
    }

    /// <summary>
    /// map the current endpoint to an api version set by specifying the api name
    /// </summary>
    /// <param name="apiName">the name of the api (swagger tag) this endpoint belongs to</param>
    /// <exception cref="InvalidOperationException">thrown when the specified api set is not found in the <see cref="VersionSets"/> container</exception>
    public static IEndpointConventionBuilder WithVersionSet(this IEndpointConventionBuilder b, string apiName)
    {
        if (VersionSets.Container.TryGetValue(apiName, out var versionSet))
            b.WithApiVersionSet(versionSet);
        else
            throw new InvalidOperationException($"A version set with name [{apiName}] has not been registered using .MapVersionSet() at startup!");

        return b;
    }

    /// <summary>
    /// specify the version of this swagger document.
    /// </summary>
    /// <param name="apiVersion">only endpoints belonging to the specified version will show up for this swagger doc</param>
    public static void ApiVersion(this AspNetCoreOpenApiDocumentGeneratorSettings s, ApiVersion apiVersion)
    {
        var strVersion = apiVersion.ToString(VersionSets.VersionFormat);
        s.ApiGroupNames = new[] { strVersion };
        s.Version = strVersion;
    }
}