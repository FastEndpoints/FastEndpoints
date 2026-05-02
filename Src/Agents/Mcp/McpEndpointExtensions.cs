namespace FastEndpoints.Mcp;

/// <summary>
/// opt-in extension methods for exposing FastEndpoints endpoints as MCP tools. add
/// <c>using FastEndpoints.Mcp;</c> to your endpoint file and call <c>this.McpTool(...)</c>
/// inside <c>Configure()</c>. the <c>this.</c> prefix is required by the C# language for an
/// extension method call on the enclosing instance — it is not a style preference, bare
/// <c>McpTool(...)</c> will not resolve. the alternative, <c>Definition.McpTool(...)</c>, is
/// useful when composing configuration from helpers that only see the endpoint definition.
/// <para>
/// the addon stores a single <see cref="McpToolInfo" /> instance on the endpoint's public
/// <see cref="EndpointDefinition.EndpointMetadata" /> bag — no modification to the core library
/// is needed. repeat calls mutate the same instance.
/// </para>
/// </summary>
public static class McpEndpointExtensions
{
    /// <summary>
    /// opt this endpoint in to being exposed as a Model Context Protocol (MCP) tool via the
    /// <c>FastEndpoints.Mcp</c> addon. without an opt-in call the endpoint is invisible to MCP
    /// clients — the safe default.
    /// </summary>
    /// <param name="ep">the endpoint (this parameter). call as <c>this.McpTool(...)</c> inside <c>Configure()</c>.</param>
    /// <param name="name">tool name seen by MCP clients. <c>null</c> uses the endpoint type name converted to <c>snake_case</c>.</param>
    /// <param name="description">description shown to LLMs when listing tools. <c>null</c> falls back to the endpoint summary / XML docs.</param>
    /// <param name="configure">optional callback for setting tool hints (read-only, idempotent, destructive, open-world).</param>
    public static void McpTool(this BaseEndpoint ep, string? name = null, string? description = null, Action<McpToolInfo>? configure = null)
        => ep.Definition.McpTool(name, description, configure);

    extension(EndpointDefinition def)
    {
        /// <summary>
        /// opt this endpoint in to being exposed as a Model Context Protocol (MCP) tool. identical to the
        /// <see cref="BaseEndpoint" /> overload but targets the <see cref="EndpointDefinition" /> directly
        /// — useful when composing configuration from helpers that only see the definition.
        /// </summary>
        public void McpTool(string? name = null,
                            string? description = null,
                            Action<McpToolInfo>? configure = null)
        {
            var info = ResolveOrCreate(def);
            if (name is not null)
                info.Name = name;
            if (description is not null)
                info.Description = description;
            configure?.Invoke(info);
        }

        /// <summary>
        /// resolve the <see cref="McpToolInfo" /> attached to an endpoint, preferring a fluent
        /// <c>McpTool(...)</c> registration (stored on <see cref="EndpointDefinition.EndpointMetadata" />)
        /// and falling back to a <see cref="McpToolAttribute" /> captured in
        /// <see cref="EndpointDefinition.EndpointAttributes" />. returns <c>null</c> when the endpoint has
        /// not opted in.
        /// </summary>
        internal McpToolInfo? ResolveToolInfo()
        {
            if (def.EndpointMetadata is { } meta)
            {
                foreach (var o in meta)
                {
                    if (o is McpToolInfo info)
                        return info;
                }
            }

            if (def.EndpointAttributes is { } attrs)
            {
                foreach (var a in attrs)
                {
                    if (a is McpToolAttribute attr)
                        return attr.ToInfo();
                }
            }

            return null;
        }
    }

    static McpToolInfo ResolveOrCreate(EndpointDefinition def)
    {
        if (def.EndpointMetadata is { } meta)
        {
            foreach (var o in meta)
            {
                if (o is McpToolInfo existing)
                    return existing;
            }
        }

        var info = new McpToolInfo();
        def.Metadata(info);

        return info;
    }
}