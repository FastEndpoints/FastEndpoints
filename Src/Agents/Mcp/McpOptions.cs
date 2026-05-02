using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints.Mcp;

/// <summary>
/// user-facing configuration for the FastEndpoints MCP bridge. passed to
/// <see cref="Extensions.AddMcp(Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Action{McpOptions}?)" />.
/// </summary>
public sealed class McpOptions
{
    /// <summary>optional filter controlling which endpoints appear as MCP tools. defaults to all opt-in endpoints.</summary>
    public Func<EndpointDefinition, bool>? ToolFilter { get; set; }

    /// <summary>
    /// optional per-caller visibility filter used on <c>tools/list</c> to hide tools the current
    /// principal cannot invoke. runs once per session; if <c>null</c>, <see cref="RequiresAuthorizationFilter" />
    /// is used as the default so auth-gated endpoints stay hidden from anonymous callers.
    /// </summary>
    public Func<EndpointDefinition, ClaimsPrincipal, HttpContext, bool>? ToolVisibilityFilter { get; set; }

    /// <summary>include <c>outputSchema</c> in tool descriptors when <c>true</c> (default).</summary>
    public bool IncludeOutputSchemas { get; set; } = true;

    /// <summary>
    /// default visibility check — allows anonymous callers to see anonymous tools and authenticated
    /// callers to see everything they could plausibly invoke. endpoints with role/policy requirements
    /// are hidden until the caller authenticates. the actual auth check happens per invocation.
    /// </summary>
    public static readonly Func<EndpointDefinition, ClaimsPrincipal, HttpContext, bool> RequiresAuthorizationFilter =
        static (def, principal, _) => !def.RequiresAuthorization() || (principal.Identity?.IsAuthenticated ?? false);
}
