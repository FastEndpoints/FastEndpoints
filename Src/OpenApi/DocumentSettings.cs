using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

/// <summary>
/// settings for the swagger document identity and security
/// </summary>
public class DocumentSettings
{
    /// <summary>
    /// the name of the swagger document (used as the route parameter)
    /// </summary>
    public string DocumentName { get; set; } = "v1";

    /// <summary>
    /// the title of the swagger document
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// the version of the swagger document
    /// </summary>
    public string? Version { get; set; }

    internal List<AuthSchemeConfig> AuthSchemes { get; } = [];

    /// <summary>
    /// add swagger auth for this open api document
    /// </summary>
    /// <param name="schemeName">the authentication scheme</param>
    /// <param name="securityScheme">an open api security scheme object</param>
    /// <param name="globalScopeNames">a collection of global scope names</param>
    public void AddAuth(string schemeName, OpenApiSecurityScheme securityScheme, IEnumerable<string>? globalScopeNames = null)
    {
        AuthSchemes.Add(new(schemeName, securityScheme, globalScopeNames));
    }
}

internal record AuthSchemeConfig(string Name, OpenApiSecurityScheme Scheme, IEnumerable<string>? GlobalScopes);