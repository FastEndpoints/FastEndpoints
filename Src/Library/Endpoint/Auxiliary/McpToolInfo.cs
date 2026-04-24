namespace FastEndpoints;

/// <summary>
/// metadata captured by the core library when an endpoint is opted-in to be exposed as a
/// Model Context Protocol (MCP) tool. this object is set on <see cref="EndpointDefinition.McpTool" />
/// either by the bare <c>McpTool(…)</c> call inside <see cref="BaseEndpoint.Configure" /> or by the
/// <c>[McpTool]</c> attribute. the <c>FastEndpoints.Mcp</c> companion package reads it to build the
/// tool descriptor exposed to MCP clients.
/// </summary>
public sealed class McpToolInfo
{
    /// <summary>
    /// unique tool name seen by MCP clients. must match <c>^[a-zA-Z0-9_-]+$</c>. defaults to
    /// the endpoint type's short name in <c>snake_case</c> when <c>null</c>.
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
    /// hints that describe the behaviour of this tool — used by MCP clients to reason about
    /// side-effects before invoking it. all fields are advisory; the server itself enforces nothing.
    /// </summary>
    public McpToolBehaviorHints Hints { get; set; } = new();
}

/// <summary>
/// advisory behavioural hints for an MCP tool. mirrors the <c>ToolAnnotations</c> structure in the
/// MCP spec so they can be forwarded unchanged by the companion package.
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
