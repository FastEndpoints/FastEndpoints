namespace FastEndpoints;

/// <summary>
/// represents an endpoint that has been discovered during startup
/// </summary>
/// <param name="EndpointType">the type of the discovered endpoint class</param>
/// <param name="Routes">the routes the endpoint will match</param>
/// <param name="Verbs">the http verbs the endpoint will be listening for</param>
/// <param name="AnonymousVerbs">the verbs which will be allowed anonymous access to</param>
/// <param name="ThrowIfValidationFails">whether automatic validation failure will be sent</param>
/// <param name="Policies">the security policies for the endpoint</param>
/// <param name="Roles">the roles which will be allowed access to</param>
/// <param name="Permissions">the permissions which will allow access</param>
/// <param name="AllowAnyPermission">whether any or all permissions will be required</param>
/// <param name="Claims">the user claim types which will allow access</param>
/// <param name="AllowAnyClaim">whether any or all claim types will be required</param>
/// <param name="Tags">the tags associated with the endpoint</param>
public record DiscoveredEndpoint(
    Type EndpointType,
    IEnumerable<string> Routes,
    IEnumerable<string> Verbs,
    IEnumerable<string>? AnonymousVerbs,
    bool ThrowIfValidationFails,
    IEnumerable<string>? Policies,
    IEnumerable<string>? Roles,
    IEnumerable<string>? Permissions,
    bool AllowAnyPermission,
    IEnumerable<string>? Claims,
    bool AllowAnyClaim,
    IEnumerable<string>? Tags);