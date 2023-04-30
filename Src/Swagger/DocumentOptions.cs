using NSwag.Generation.AspNetCore;
using System.Text.Json;

namespace FastEndpoints.Swagger;

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
    /// a function for configuring the swagger document generator settings
    /// </summary>
    public Action<AspNetCoreOpenApiDocumentGeneratorSettings>? DocumentSettings { get; set; }
    /// <summary>
    /// by default GET request DTO properties are automatically converted to query parameters because fetch-client/swagger ui doesn't support it.
    /// set this to true if for some reason you'd like to disable this auto convertion and allow GET requests with a body.
    /// </summary>
    public bool EnableGetRequestsWithBody { get; set; }
    /// <summary>
    /// set to false to disable auto addition of jwt bearer auth support
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
    /// enabling this flattens the inheritance hierachy of all the schmema.
    /// </summary>
    public bool FlattenSchema { get; set; }
    /// <summary>
    /// endpoints greater than this version will not be included in the swagger doc.
    /// </summary>
    public int MaxEndpointVersion { get; set; }
    /// <summary>
    /// endpoints lower than this vesion will not be included in the swagger doc.
    /// </summary>
    public int MinEndpointVersion { get; set; }
    /// <summary>
    /// set to true for removing empty request dto schema from the swagger document.
    /// <para>WARNING: enabling this also flattens the inheritance hierachy of the schmema.</para>
    /// </summary>
    public bool RemoveEmptyRequestSchema { get; set; }
    /// <summary>
    /// json serializer options
    /// </summary>
    public Action<JsonSerializerOptions>? SerializerSettings { get; set; }
    /// <summary>
    /// set to true if you'd like schema names to be just the class name instead of the full name.
    /// </summary>
    public bool ShortSchemaNames { get; set; }
    /// <summary>
    /// the casing strategy to use when naming endpoint tags.
    /// </summary>
    public TagCase TagCase { get; set; } = TagCase.TitleCase;
    /// <summary>
    /// specify swagger tag descriptions for the document.
    /// the key of the dictionary is the name of the tag to add a description for.
    /// </summary>
    public Action<Dictionary<string, string>>? TagDescriptions { get; set; }
}