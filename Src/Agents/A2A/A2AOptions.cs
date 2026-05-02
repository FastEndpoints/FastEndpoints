using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints.A2A;

/// <summary>
/// configuration for <see cref="Extensions.AddA2A" /> / <see cref="Extensions.UseA2A" />.
/// surfaces the agent-card fields that sit at <c>/.well-known/agent-card.json</c> plus the JSON-RPC route.
/// </summary>
public sealed class A2AOptions
{
    /// <summary>human-readable agent name shown to orchestrators. required.</summary>
    public string AgentName { get; set; } = "fastendpoints-agent";

    /// <summary>agent description for discovery. shown alongside skills in the agent card.</summary>
    public string? Description { get; set; }

    /// <summary>agent version; surfaced in the agent card.</summary>
    public string Version { get; set; } = "0.1.0";

    /// <summary>the organization publishing this agent (provider info on the agent card).</summary>
    public A2AProvider? Provider { get; set; }

    /// <summary>public JSON-RPC URL for this agent as seen by remote agents. defaults to the request base plus <c>/a2a</c> if <c>null</c>.</summary>
    public string? Url { get; set; }

    /// <summary>optional startup/static filter controlling which opt-in endpoints are published as skills.</summary>
    public Func<EndpointDefinition, bool>? SkillFilter { get; set; }

    /// <summary>
    /// per-caller visibility filter used on the agent card and skill dispatch to hide skills the current agent caller
    /// cannot invoke. FastEndpoints REST endpoint authorization is intentionally not reused by A2A; configure the A2A
    /// routes and this agent-facing filter separately. anonymous callers are denied by default. set this to a custom
    /// delegate such as <c>(_, _, _) => true</c> to relax visibility.
    /// </summary>
    public Func<EndpointDefinition, ClaimsPrincipal, HttpContext, bool> SkillVisibilityFilter { get; set; } = _authenticatedCallersOnly;

    internal string RpcPattern { get; set; } = "/a2a";

    static readonly Func<EndpointDefinition, ClaimsPrincipal, HttpContext, bool> _authenticatedCallersOnly =
        static (_, principal, _) => principal.Identity?.IsAuthenticated == true;
}

/// <summary>agent-card <c>provider</c> block.</summary>
public sealed class A2AProvider
{
    [System.Text.Json.Serialization.JsonPropertyName("organization")]
    public string Organization { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("url")]
    public string? Url { get; set; }
}