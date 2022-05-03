using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace FastEndpoints;

/// <summary>
/// represents the configuration settings of an endpoint
/// </summary>
public sealed class EndpointDefinition
{
    public string[]? Routes { get; internal set; }
    public string[]? Verbs { get; internal set; }
    public string[]? AnonymousVerbs { get; internal set; }
    public bool ThrowIfValidationFails { get; internal set; } = true;
    public bool AllowFormData { get; internal set; }
    public bool DontBindFormData { get; internal set; }
    public string[]? PreBuiltUserPolicies { get; internal set; }
    public string[]? AuthSchemes { get; internal set; }
    public string[]? Roles { get; internal set; }
    public string[]? Permissions { get; internal set; }
    public bool AllowAnyPermission { get; internal set; }
    public string[]? ClaimTypes { get; internal set; }
    public bool AllowAnyClaim { get; internal set; }
    public string[]? Tags { get; internal set; }
    public EndpointSummary? Summary { get; internal set; }
    public EpVersion Version { get; internal set; } = new();
    public string SecurityPolicyName => $"epPolicy:{EndpointType.FullName}";
    public string? RoutePrefixOverride { get; internal set; }
    public bool DontAutoTag { get; internal set; }
    public Type ReqDtoType { get; internal set; }
    public Type EndpointType { get; internal set; }
    public Type? ValidatorType { get; internal set; }
    public bool ScopedValidator { get; internal set; }

    internal ServiceBoundEpProp[]? ServiceBoundEpProps;
    internal Action<RouteHandlerBuilder> InternalConfigAction;
    internal Action<RouteHandlerBuilder>? UserConfigAction;
    internal object? PreProcessors;
    internal object? PostProcessors;
    internal ResponseCacheAttribute? ResponseCacheSettings;
    internal HitCounter? HitCounter;
    internal JsonSerializerContext? SerializerContext;
    internal bool ExecuteAsyncImplemented;
}

/// <summary>
/// represents an enpoint version
/// </summary>
public sealed class EpVersion
{
    public int Current { get; internal set; }
    public int DeprecatedAt { get; internal set; }
}

internal sealed class ServiceBoundEpProp
{
    public Type PropType { get; set; }
    public Action<object, object> PropSetter { get; set; }
}