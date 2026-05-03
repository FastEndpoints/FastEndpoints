namespace FastEndpoints.Mcp;

/// <summary>
/// opt-in metadata attached to a FastEndpoints endpoint so the <c>FastEndpoints.Mcp</c> addon
/// exposes it as a Model Context Protocol (MCP) tool. pushed onto the endpoint's public
/// <see cref="EndpointDefinition.EndpointMetadata" /> bag via the extension methods in
/// <see cref="McpEndpointExtensions" />, or materialized at startup from the <see cref="McpToolAttribute" />.
/// core FastEndpoints has no knowledge of this type.
/// </summary>
public sealed class McpToolInfo
{
    /// <summary>
    /// unique tool name seen by MCP clients. explicit values must match <c>^[a-zA-Z0-9_-]+$</c>.
    /// when <c>null</c>, the name is generated from the endpoint summary or type name and
    /// sanitized to the same character set.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// human-readable description shown to LLMs when listing available tools. a rich description
    /// helps the model pick the right tool and supply the right arguments.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// optional title shown in UIs that list MCP tools (e.g. inspector UIs).
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// hints that describe the behavior of this tool — used by MCP clients to reason about
    /// side effects before invoking it. all fields are advisory; the server itself enforces nothing.
    /// </summary>
    public McpToolBehaviorHints Hints { get; set; } = new();
}

/// <summary>
/// advisory behavioral hints for an MCP tool. mirrors the <c>ToolAnnotations</c> structure in the
/// MCP spec so they can be forwarded unchanged to clients.
/// </summary>
public sealed class McpToolBehaviorHints
{
    /// <summary>
    /// <c>true</c> if the tool does not modify its environment. GET-style endpoints should set this.
    /// </summary>
    public bool? ReadOnly { get; set; }

    /// <summary>
    /// <c>true</c> if calling the tool repeatedly with the same arguments yields the same effect.
    /// </summary>
    public bool? Idempotent { get; set; }

    /// <summary>
    /// <c>true</c> if the tool may perform irreversible actions (delete, publish, etc.).
    /// </summary>
    public bool? Destructive { get; set; }

    /// <summary>
    /// <c>true</c> if the tool interacts with systems outside the process (network, external APIs).
    /// </summary>
    public bool? OpenWorld { get; set; }
}
