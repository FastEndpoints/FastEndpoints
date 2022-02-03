using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

namespace FastEndpoints;

/// <summary>
/// represents the configuration settings of an endpoint
/// </summary>
public class EndpointSettings
{
    public string[]? Routes;
    public string[]? Verbs;
    public string[]? AnonymousVerbs;
    public bool ThrowIfValidationFails = true;
    public string[]? PreBuiltUserPolicies;
    public string[]? AuthSchemes;
    public string[]? Roles;
    public string[]? Permissions;
    public bool AllowAnyPermission;
    public string[]? ClaimTypes;
    public bool AllowAnyClaim;
    public string[]? Tags;
    public EpVersion Version = new();
    public EndpointSummary? Summary;

    internal Type? DtoTypeForFormData;
    internal Action<RouteHandlerBuilder> InternalConfigAction;
    internal Action<RouteHandlerBuilder>? UserConfigAction;
    internal object? PreProcessors;
    internal object? PostProcessors;
    internal ResponseCacheAttribute? ResponseCacheSettings;
}

/// <summary>
/// represents an enpoint version
/// </summary>
public struct EpVersion
{
    public int Current { get; set; }
    public int DeprecatedAt { get; set; }
}