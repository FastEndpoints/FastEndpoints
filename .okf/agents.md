---
type: Reference
title: Agents Compatibility
description: MCP behavior and the binary contract between independently versioned Agents addons and core.
tags: [gotcha, architecture]
---

# Agents Compatibility

- **MCP refusal auditing:** `OnToolCallRefused` is call-path-only, not a side effect of `ToolVisibilityFilter` or listing. Report once only after no visible name candidate resolves, or at the invocation recheck. Hidden duplicates must not audit a successful call to a visible candidate. Keep the capturing audit helper separate from the callback-null fast path. Configure auditing before resolution, not inside the visibility filter; the invocation recheck reads the current callback separately. Contract and usage: `../FE-Docs/src/content/docs/27-ai-agents.md`.
- **MCP schema descriptions:** `JsonSchemaBuilder` applies `DescriptionAttribute` to input/output properties while preserving JSON names and nested schemas. Source-generated metadata lacks `AttributeProvider`, so resolve the public property through `JsonPropertyInfo.DeclaringType` and its serialized name. Omit empty descriptions; convert boolean schemas to equivalent object schemas before annotation. Tests: `JsonSchemaBuilderTests`, `McpToolSchemaRootTests`.
- **Agents versioning:** `Src/Agents/Directory.Build.props` must `Import` the parent props. MSBuild stops at the first `Directory.Build.props`. Shared agent code is linked compile, not a NuGet.
- **Agents friend-assembly binary contract:** `InternalsVisibleTo` in `Src/Library/Metadata.cs` for `FastEndpoints.Mcp` and `FastEndpoints.A2A` (independent versions). Recompiling addons here does not prove published NuGets still load. Changing the name, arity, parameter types, or return type of a stocked internal can `MissingMethodException` shipped addons. Touch any of these in Library: check the agent packages and restock this list in the same change.
  - **Reflection / binding** (`BinderExtensions.cs`, `ReflectionExtensions.cs`): `Type.BindableProps()` (return type is part of the CLR signature), `PropertyInfo.FieldName()`, `Type.IsComplexType()`, `Type.IsCollection()`.
  - **Bootstrap / discovery** (`EndpointBootstrap.cs`, `EndpointData.cs`, `MainExtensions.cs`): `EndpointBootstrap.CreateEndpoint(HttpContext, EndpointDefinition)`, `EndpointData.Found`, `MainExtensions.BuildRoute(StringBuilder, int, string, string?)`. Do not add a `BuildRoute` parameter. `DontVersion()` is how version 0 omits the segment.
  - **EndpointDefinition fields:** `SerializerContext`, `Disposable`, `DisposableAsync`.
  - **Config / type tokens:** `Config.SerOpts` (also `using static FastEndpoints.Config`), and `Types` fields agents use: `EmptyRequest`, `FromBodyAttribute`, `ToHeaderAttribute`, `String`.
  - **Call sites to check when restocking:** `Src/Agents/Shared/` (`AgentRequestBuilder.cs`, `AgentJsonPropertyNames.cs`, `AgentHttpContextFactory.cs`, `EndpointInvoker.cs`, `AgentEndpointCatalog.cs`), `Src/Agents/Mcp/` (`McpToolSchemaFactory.cs`, `EndpointMcpToolSource.cs`), `Src/Agents/A2A/` (`A2AJsonRpcEndpoint.cs`, `A2ASkillDispatcher.cs`, `Extensions.cs`).

## Sources
- `Src/Library/Metadata.cs`
- `Src/Agents/Shared/`
- `Src/Agents/Mcp/`
- `Src/Agents/A2A/`
- `Src/Agents/Directory.Build.props`
