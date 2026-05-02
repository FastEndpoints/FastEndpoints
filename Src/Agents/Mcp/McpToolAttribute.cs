using System.Reflection;

namespace FastEndpoints.Mcp;

/// <summary>
/// opt an endpoint in to being exposed as a Model Context Protocol (MCP) tool. the
/// <c>FastEndpoints.Mcp</c> addon scans the endpoint's public <see cref="EndpointDefinition.EndpointAttributes" />
/// for this attribute at <c>UseMcp()</c> time. use this form on attribute-configured endpoints; endpoints
/// that override <c>Configure()</c> should call <c>McpEndpointExtensions.McpTool(...)</c>
/// instead. core FastEndpoints has no knowledge of this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class McpToolAttribute : Attribute
{
    /// <inheritdoc cref="McpToolAttribute" />
    public McpToolAttribute(string? name = null)
    {
        Name = name;
    }

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

    internal McpToolInfo ToInfo(Type endpointType)
    {
        var info = new McpToolInfo
        {
            Name = Name,
            Description = Description,
            Title = Title
        };

        var configuredHints = endpointType.GetCustomAttributesData()
                                         .FirstOrDefault(a => a.AttributeType == typeof(McpToolAttribute))?
                                         .NamedArguments
                                         .Select(a => a.MemberName)
                                         .ToHashSet(StringComparer.Ordinal);

        if (configuredHints?.Contains(nameof(ReadOnly)) == true)
            info.Hints.ReadOnly = ReadOnly;
        if (configuredHints?.Contains(nameof(Idempotent)) == true)
            info.Hints.Idempotent = Idempotent;
        if (configuredHints?.Contains(nameof(Destructive)) == true)
            info.Hints.Destructive = Destructive;
        if (configuredHints?.Contains(nameof(OpenWorld)) == true)
            info.Hints.OpenWorld = OpenWorld;

        return info;
    }
}
