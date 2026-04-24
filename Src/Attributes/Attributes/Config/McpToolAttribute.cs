namespace FastEndpoints;

/// <summary>
/// opt an endpoint in to being exposed as a Model Context Protocol (MCP) tool. equivalent to
/// calling <c>McpTool(…)</c> from inside <c>Configure()</c>. processed only on endpoints that rely
/// on attribute-style configuration (i.e. no <c>Configure()</c> override); endpoints that override
/// <c>Configure()</c> should call <c>McpTool(…)</c> there instead.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class McpToolAttribute : Attribute
{
    /// <summary>
    /// tool name seen by MCP clients. <c>null</c> uses the endpoint type name converted to <c>snake_case</c>.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// description shown to LLMs when listing tools.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// optional display title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// advisory: tool does not modify its environment.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// advisory: repeated calls with the same args produce the same effect.
    /// </summary>
    public bool Idempotent { get; set; }

    /// <summary>
    /// advisory: tool may perform irreversible actions.
    /// </summary>
    public bool Destructive { get; set; }

    /// <summary>
    /// advisory: tool touches the outside world (network, external services).
    /// </summary>
    public bool OpenWorld { get; set; }

    /// <inheritdoc cref="McpToolAttribute" />
    public McpToolAttribute(string? name = null)
    {
        Name = name;
    }
}
