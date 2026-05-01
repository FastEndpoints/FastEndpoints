# FastEndpoints agent addons

This directory holds **addon packages** that bridge FastEndpoints into two agent
protocols. They are published under the FastEndpoints NuGet account but are
**versioned independently** of the main `FastEndpoints` package - no coordinated
release cadence, no forced bump on either side.

| Package | Version | Protocol |
|---|---|---|
| `FastEndpoints.Mcp` | `1.0.0-beta.1` | Model Context Protocol (stdio + Streamable HTTP) |
| `FastEndpoints.A2A` | `0.1.0-preview.1` | A2A (agent-to-agent) - Agent Card + JSON-RPC 2.0 |

## Design

The main `FastEndpoints` library carries **no AI-specific public API surface**.
Opt-in to either protocol is done entirely from the addon assembly:

```csharp
// fluent form - inside Configure():
public override void Configure()
{
    Get("/orders/{id}");
    this.McpTool("get_order", "Retrieve an order by id.");   // FastEndpoints.Mcp
    // or: this.A2ASkill("get_order", tags: ["orders"]);     // FastEndpoints.A2A
}

// attribute form:
[McpTool("get_order", Description = "Retrieve an order by id.")]
public class GetOrder : Endpoint<GetOrderRequest, Order> { /* ... */ }
```

The `this.` prefix is **required by the C# language** on an extension method
invoked against the enclosing instance — bare `McpTool(...)` does not resolve
inside `Configure()` because the compiler only searches for extension methods
when there is an explicit receiver. The alternative was to ship parallel
`McpEndpoint<,>` / `A2AEndpoint<,>` base classes, which would force a choice
of inheritance on every opt-in endpoint for the sake of saving four characters,
so we keep the simpler extension form. The owner explicitly accepted this:
*"AI lovers will have to do `this.Whatever()` to configure it"* (Discord).

Behind the scenes the addon pushes an `McpToolInfo` / `A2ASkillInfo` into the
endpoint's existing `EndpointDefinition.EndpointMetadata` bag (for the fluent
form) or reads back the addon attribute from `EndpointDefinition.EndpointAttributes`
(for the attribute form). Both are generic, AI-agnostic extension points that
FastEndpoints already exposes to every addon. Authors composing configuration
from helpers can also call `Definition.McpTool(...)` / `Definition.A2ASkill(...)`
(extension on `EndpointDefinition`) directly.

## Layout

```
Src/Agents/
  Directory.Build.props      shared PackageTags; imports parent Src/Directory.Build.props
  Shared/                    compile-included internal plumbing (not a published package)
    EndpointInvoker.cs       runs an endpoint from inside a non-ASP.NET call path
    JsonSchemaBuilder.cs     DTO -> JSON Schema
    FluentValidationSchemaEnricher.cs   FluentValidation -> JSON Schema constraints
    AssemblyAttributes.cs    InternalsVisibleTo Int.Agents for each addon
  Mcp/                       FastEndpoints.Mcp package
  A2A/                       FastEndpoints.A2A package
```

The `Shared/*.cs` files are included into both addon csprojs as `<Compile Include>`
so each addon ships the plumbing as **internal types** - no third NuGet package,
no public type collisions if a consumer references both addons.

## Independent versioning

`Src/Agents/Directory.Build.props` imports the parent `Src/Directory.Build.props`
(for shared LangVersion / Nullable / signing / source link) and only overrides
the `<PackageTags>`. Each addon csproj pins its own `<Version>`.

## Internals access

The core `FastEndpoints` library grants `InternalsVisibleTo` to each addon
assembly in `Src/Library/Metadata.cs`. The `EndpointInvoker` needs this to
reach the internal setters on `BaseEndpoint.Definition` / `BaseEndpoint.HttpContext`
and to call `ExecAsync`. No public API in `FastEndpoints` is involved, in line
with the owner's constraint that addon packages may use internal plumbing but
must not add AI-named types to the main library.

## Tests

`Tests/IntegrationTests/FastEndpoints.Agents/Int.Agents.csproj` references both
addons. The A2A project is aliased as `A2AAsm` there so the shared internal
plumbing (which exists in both assemblies) resolves unambiguously to the Mcp
copy by default.
