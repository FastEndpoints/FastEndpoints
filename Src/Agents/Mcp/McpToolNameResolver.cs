using FastEndpoints.Agents;

namespace FastEndpoints.Mcp;

static class McpToolNameResolver
{
    internal static string ResolvePublishedName(EndpointDefinition def, McpToolInfo info)
        => AgentPublishedNameResolver.Resolve(def, info.Name, "MCP tool name", "MCP tool names");
}
