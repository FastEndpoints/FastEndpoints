using FastEndpoints.Agents;
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
    /// per-caller visibility filter used by MCP tool invocation to hide tools the current agent caller cannot invoke.
    /// FastEndpoints REST endpoint authorization is intentionally not reused by MCP; configure the MCP route and this
    /// agent-facing filter separately. anonymous callers are denied by default. set this to a custom delegate such as
    /// <c>(_, _, _) => true</c> to relax visibility.
    /// </summary>
    public Func<EndpointDefinition, ClaimsPrincipal, HttpContext, bool> ToolVisibilityFilter { get; set; } = AgentVisibilityFilters.AuthenticatedCallersOnly;

    /// <summary>include <c>outputSchema</c> in tool descriptors when <c>true</c> (default).</summary>
    public bool IncludeOutputSchemas { get; set; } = true;
}
