namespace FastEndpoints.OpenApi;

/// <summary>
/// options for the swagger document
/// </summary>
public class DocumentOptions
{
    /// <summary>
    /// the index of the route path segment to use for tagging/grouping endpoints. set 0 to disable auto tagging.
    /// </summary>
    public int AutoTagPathSegmentIndex { get; set; } = 1;

    /// <summary>
    /// a function for configuring the document settings (name, title, version, auth)
    /// </summary>
    public Action<DocumentSettings>? DocumentSettings { get; set; }

    /// <summary>
    /// by default GET request DTO properties are automatically converted to query parameters because fetch-client/swagger ui doesn't support it.
    /// set this to true if for some reason you'd like to disable this auto conversion and allow GET requests with a body.
    /// </summary>
    public bool EnableGetRequestsWithBody { get; set; }

    /// <summary>
    /// set to <c>false</c> to disable auto addition of jwt bearer auth support
    /// </summary>
    public bool EnableJWTBearerAuth { get; set; } = true;

    /// <summary>
    /// a function to filter out endpoints from the swagger document.
    /// this function will be run against every fast endpoint discovered.
    /// return true to include the endpoint and return false to exclude the endpoint from the swagger doc.
    /// </summary>
    public Func<EndpointDefinition, bool>? EndpointFilter { get; set; }

    /// <summary>
    /// if set to true, only FastEndpoints will show up in the swagger doc
    /// </summary>
    public bool ExcludeNonFastEndpoints { get; set; }

    /// <summary>
    /// enabling this flattens the inheritance hierarchy of all the schema.
    /// </summary>
    public bool FlattenSchema { get; set; }

    /// <summary>
    /// endpoints greater than this version will not be included in this swagger doc.
    /// </summary>
    public int MaxEndpointVersion { get; set; }

    /// <summary>
    /// endpoints lower than this version will not be included in the swagger doc.
    /// </summary>
    public int MinEndpointVersion { get; set; }

    /// <summary>
    /// specify a "release version" for this swagger document.
    /// </summary>
    public int ReleaseVersion { get; set; }

    /// <summary>
    /// by default deprecated endpoints/operations will not show up in the swagger doc.
    /// set this to true if you instead want them to show up but displayed as "obsolete".
    /// </summary>
    public bool ShowDeprecatedOps { get; set; }

    /// <summary>
    /// set to true for removing empty request dto schema from the swagger document.
    /// </summary>
    public bool RemoveEmptyRequestSchema { get; set; }

    /// <summary>
    /// set to true if you'd like schema names to be just the class name instead of the full name.
    /// </summary>
    public bool ShortSchemaNames { get; set; }

    /// <summary>
    /// the casing strategy to use when naming endpoint tags.
    /// </summary>
    public TagCase TagCase { get; set; } = TagCase.TitleCase;

    /// <summary>
    /// specify whether to strip non-alphanumeric characters from tags.
    /// </summary>
    public bool TagStripSymbols { get; set; } = false;

    /// <summary>
    /// specify swagger tag descriptions for the document.
    /// the key of the dictionary is the name of the tag to add a description for.
    /// </summary>
    public Action<Dictionary<string, string>>? TagDescriptions { get; set; }

    /// <summary>
    /// specify if JsonSerializerOptions.PropertyNamingPolicy should be used for identifying/matching schema properties. default is 'true'.
    /// </summary>
    public bool UsePropertyNamingPolicy { get; set; } = true;

    /// <summary>
    /// by setting this to true, you can have base class types as request/response dtos and get swagger to generate possible derived types within a oneOf field.
    /// </summary>
    public bool UseOneOfForPolymorphism { get; set; }

    /// <summary>
    /// advanced configuration of the underlying OpenApiOptions.
    /// </summary>
    public Action<Microsoft.AspNetCore.OpenApi.OpenApiOptions>? ConfigureOpenApi { get; set; }

    /// <summary>
    /// gives access to the application's <see cref="IServiceProvider" /> at the time the document is being generated.
    /// this is populated by the framework when the first transformer runs and is unavailable during the
    /// initial <see cref="Extensions.OpenApiDocument(Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Action{DocumentOptions}?)" /> call.
    /// use it from inside transformers or from <see cref="ConfigureOpenApi" /> hooks that run at document generation time.
    /// </summary>
    public IServiceProvider? Services { get; internal set; }
}