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
    /// set this to true if for some reason you'd like to disable this auto conversion and allow GET requests with a body.
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
    /// enabling this flattens the inheritance hierarchy of all the schema.
    /// </summary>
    public bool FlattenSchema { get; set; }

    /// <summary>
    /// endpoints greater than this version will not be included in the swagger doc.
    /// </summary>
    public int MaxEndpointVersion { get; set; }

    /// <summary>
    /// endpoints lower than this version will not be included in the swagger doc.
    /// </summary>
    public int MinEndpointVersion { get; set; }

    /// <summary>
    /// by default deprecated endpoints/operations will not show up in the swagger doc.
    /// set this to true if you instead want them to show up but displayed as "obsolete".
    /// </summary>
    public bool ShowDeprecatedOps { get; set; }

    /// <summary>
    /// set to true for removing empty request dto schema from the swagger document.
    /// <para>WARNING: enabling this also flattens the inheritance hierarchy of the schema.</para>
    /// </summary>
    public bool RemoveEmptyRequestSchema { get; set; }

    /// <summary>
    /// json serializer options
    /// </summary>
    public Action<JsonSerializerOptions>? SerializerSettings { get; set; }

    /// <summary>
    /// any additional newtonsoft serializer settings. most useful for registering custom converters.
    /// </summary>
    public Action<Newtonsoft.Json.JsonSerializerSettings>? NewtonsoftSettings { get; set; }

    /// <summary>
    /// set to true if you'd like schema names to be just the class name instead of the full name.
    /// </summary>
    public bool ShortSchemaNames { get; set; }

    /// <summary>
    /// the casing strategy to use when naming endpoint tags.
    /// </summary>
    public TagCase TagCase { get; set; } = TagCase.TitleCase;

    /// <summary>
    /// specify whether to strip non alpha-numeric characters from tags.
    /// </summary>
    public bool TagStripSymbols { get; set; } = false;

    /// <summary>
    /// specify swagger tag descriptions for the document.
    /// the key of the dictionary is the name of the tag to add a description for.
    /// </summary>
    public Action<Dictionary<string, string>>? TagDescriptions { get; set; }

    /// <summary>
    /// specify if <see cref="JsonSerializerOptions.PropertyNamingPolicy" /> should be used by the default swagger operation processor for
    /// identifying/matching schema properties. default is 'true'.
    /// </summary>
    public bool UsePropertyNamingPolicy { get; set; } = true;

    /// <summary>
    /// by setting this to <c>true</c>, you can have base class types as request/response dtos and get swagger to generate possible derived types within a `oneOf` field.
    /// for this to take effect, you must correctly annotate the base type as follows:
    /// <code>
    /// [JsonPolymorphic(TypeDiscriminatorPropertyName = "_t")]
    /// [JsonDerivedType(typeof(Apple), "a")]
    /// [JsonDerivedType(typeof(Orange), "o")]
    /// public class FruitBase
    /// {
    ///     ...
    /// }
    /// </code>
    /// </summary>
    public bool UseOneOfForPolymorphism { get; set; }
}