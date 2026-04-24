namespace FastEndpoints.A2A;

/// <summary>
/// configuration for <see cref="Extensions.AddFastEndpointsA2A" /> / <see cref="Extensions.MapFastEndpointsA2A" />.
/// surfaces the agent-card fields that sit at <c>/.well-known/agent.json</c> plus the JSON-RPC route.
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

    /// <summary>public URL for this agent as seen by remote agents. defaults to the request base if <c>null</c>.</summary>
    public string? Url { get; set; }

    /// <summary>optional filter controlling which opt-in endpoints are published as skills.</summary>
    public Func<EndpointDefinition, bool>? SkillFilter { get; set; }
}

/// <summary>agent-card <c>provider</c> block.</summary>
public sealed class A2AProvider
{
    public string Organization { get; set; } = "";
    public string? Url { get; set; }
}
