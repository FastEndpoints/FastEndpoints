using System.Security.Claims;
using System.Text.Json;
using FastEndpoints.Mcp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FastEndpoints.Agents.Tests;

public class McpToolDuplicateNameTests
{
    [Fact]
    public void Duplicate_visible_tool_names_fail_fast_during_build()
    {
        using var provider = BuildServices((_, _, _) => true, includeSecondDuplicate: true, secondDuplicateVisible: true);
        var source = provider.GetRequiredService<EndpointMcpToolSource>();

        var ex = Should.Throw<InvalidOperationException>(() => source.BuildTools());

        ex.Message.ShouldContain("Duplicate MCP tool names detected");
        ex.Message.ShouldContain("'shared_tool'");
        ex.Message.ShouldContain(typeof(VisibleDuplicateToolEndpoint).FullName!);
        ex.Message.ShouldContain(typeof(SecondDuplicateToolEndpoint).FullName!);
    }

    [Fact]
    public async Task Hidden_duplicate_does_not_shadow_visible_tool()
    {
        using var provider = BuildServices(HideSecondDuplicate, includeSecondDuplicate: true, secondDuplicateVisible: false);
        SetUser(provider, true);

        var listed = await ListTools(provider, authenticated: true);
        listed.Tools.Select(t => t.Name).ShouldBe(["shared_tool"]);

        var result = await CallTool(provider, "shared_tool", authenticated: true);

        ((TextContentBlock)result.Content[0]).Text.ShouldContain("visible:ping");
        ((TextContentBlock)result.Content[0]).Text.ShouldNotContain("duplicate:ping");
    }

    [Fact]
    public async Task Multiple_visible_duplicates_do_not_resolve_arbitrarily_on_call_or_list()
    {
        using var provider = BuildServices((_, _, _) => true, includeSecondDuplicate: true, secondDuplicateVisible: true);
        SetUser(provider, true);

        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;

        var listEx = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await options.Handlers.ListToolsHandler!(McpToolVisibilityTests_Bridge.BuildListRequestContext(provider, BuildPrincipal(true)), CancellationToken.None));

        listEx.Message.ShouldContain("Duplicate MCP tool names detected");
        listEx.Message.ShouldContain("'shared_tool'");

        var callEx = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await options.Handlers.CallToolHandler!(McpToolVisibilityTests_Bridge.BuildCallRequestContext(provider, "shared_tool", BuildPrincipal(true)), CancellationToken.None));

        callEx.Message.ShouldContain("Duplicate MCP tool name 'shared_tool'");
    }

    static ServiceProvider BuildServices(Func<EndpointDefinition, ClaimsPrincipal, HttpContext, bool> visibilityFilter,
                                         bool includeSecondDuplicate,
                                         bool secondDuplicateVisible)
    {
        Factory.RegisterTestServices(
            s =>
            {
                s.AddSingleton(typeof(IRequestBinder<>), typeof(RequestBinder<>));
            });

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddFastEndpoints(
            o =>
            {
                o.SourceGeneratorDiscoveredTypes.Add(typeof(VisibleDuplicateToolEndpoint));
                if (includeSecondDuplicate)
                    o.SourceGeneratorDiscoveredTypes.Add(typeof(SecondDuplicateToolEndpoint));
            });
        services.AddMcp(o => o.ToolVisibilityFilter = visibilityFilter);

        var provider = services.BuildServiceProvider();

        foreach (var def in provider.GetRequiredService<EndpointData>().Found)
        {
            if (def.EndpointType == typeof(VisibleDuplicateToolEndpoint))
                def.McpTool("shared_tool");

            if (includeSecondDuplicate && def.EndpointType == typeof(SecondDuplicateToolEndpoint))
            {
                def.McpTool(
                    "shared_tool",
                    configure: info =>
                    {
                        info.Title = secondDuplicateVisible ? "Second Visible Duplicate" : "Hidden Duplicate";
                    });
            }
        }

        return provider;
    }

    static async Task<ListToolsResult> ListTools(IServiceProvider provider, bool authenticated)
    {
        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;

        return await options.Handlers.ListToolsHandler!(McpToolVisibilityTests_Bridge.BuildListRequestContext(provider, BuildPrincipal(authenticated)), CancellationToken.None);
    }

    static async Task<CallToolResult> CallTool(IServiceProvider provider, string toolName, bool authenticated)
    {
        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;

        return await options.Handlers.CallToolHandler!(McpToolVisibilityTests_Bridge.BuildCallRequestContext(provider, toolName, BuildPrincipal(authenticated)), CancellationToken.None);
    }

    static ClaimsPrincipal BuildPrincipal(bool authenticated)
        => authenticated
               ? new(new ClaimsIdentity([new("sub", "caller")], "test"))
               : new(new ClaimsIdentity());

    static void SetUser(IServiceProvider provider, bool authenticated)
    {
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            RequestServices = provider,
            User = BuildPrincipal(authenticated)
        };
    }

    static bool HideSecondDuplicate(EndpointDefinition def, ClaimsPrincipal _, HttpContext __)
        => def.EndpointType != typeof(SecondDuplicateToolEndpoint);

    [HttpPost("/visible-duplicate-tool")]
    sealed class VisibleDuplicateToolEndpoint : Endpoint<ToolRequest, ToolResponse>
    {
        public override Task HandleAsync(ToolRequest req, CancellationToken ct)
            => Send.OkAsync(new() { Value = "visible:" + req.Value }, ct);
    }

    [HttpPost("/second-duplicate-tool")]
    sealed class SecondDuplicateToolEndpoint : Endpoint<ToolRequest, ToolResponse>
    {
        public override Task HandleAsync(ToolRequest req, CancellationToken ct)
            => Send.OkAsync(new() { Value = "duplicate:" + req.Value }, ct);
    }

    sealed class ToolRequest
    {
        public string Value { get; set; } = "";
    }

    sealed class ToolResponse
    {
        public string? Value { get; set; }
    }
}
