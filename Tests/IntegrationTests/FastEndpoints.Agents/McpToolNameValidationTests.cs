using System.Security.Claims;
using System.Text.Json;
using FastEndpoints.Mcp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FastEndpoints.Agents.Tests;

public class McpToolNameValidationTests
{
    [Theory]
    [InlineData("bad name")]
    [InlineData("bad.name")]
    [InlineData("")]
    public void Explicit_invalid_tool_names_fail_with_clear_exception(string invalidName)
    {
        using var provider = BuildServices(typeof(ExplicitInvalidNameEndpoint));
        GetDefinition<ExplicitInvalidNameEndpoint>(provider).McpTool(invalidName);

        var ex = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<EndpointMcpToolSource>().BuildTools());

        ex.Message.ShouldContain("Invalid explicit MCP tool name");
        ex.Message.ShouldContain($"'{invalidName}'");
        ex.Message.ShouldContain(typeof(ExplicitInvalidNameEndpoint).FullName!);
    }

    [Fact]
    public void Attribute_invalid_tool_name_fails_with_clear_exception()
    {
        using var provider = BuildServices(typeof(AttributeInvalidNameEndpoint));

        var ex = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<EndpointMcpToolSource>().BuildTools());

        ex.Message.ShouldContain("Invalid explicit MCP tool name");
        ex.Message.ShouldContain("'bad.name'");
        ex.Message.ShouldContain(typeof(AttributeInvalidNameEndpoint).FullName!);
    }

    [Fact]
    public void Summary_derived_name_is_normalized_to_valid_stable_name()
    {
        using var provider = BuildServices(typeof(SummaryDerivedNameEndpoint));
        ConfigureGeneratedTool<SummaryDerivedNameEndpoint>(provider, "Get user (admin-only)");

        var tool = provider.GetRequiredService<EndpointMcpToolSource>().BuildTools().Single();

        tool.ProtocolTool.Name.ShouldBe("get_user_admin_only");
    }

    [Fact]
    public void Generated_name_that_normalizes_to_empty_fails_clearly()
    {
        using var provider = BuildServices(typeof(EmptyGeneratedNameEndpoint));
        ConfigureGeneratedTool<EmptyGeneratedNameEndpoint>(provider, "!!!");

        var ex = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<EndpointMcpToolSource>().BuildTools());

        ex.Message.ShouldContain("Generated MCP tool name");
        ex.Message.ShouldContain(typeof(EmptyGeneratedNameEndpoint).FullName!);
        ex.Message.ShouldContain("Bad value: '!!!'");
    }

    [Fact]
    public void Different_inputs_that_normalize_to_same_final_name_fail_duplicate_check()
    {
        using var provider = BuildServices(typeof(FirstDuplicateGeneratedNameEndpoint), typeof(SecondDuplicateGeneratedNameEndpoint));
        ConfigureGeneratedTool<FirstDuplicateGeneratedNameEndpoint>(provider, "Hello.World");
        ConfigureGeneratedTool<SecondDuplicateGeneratedNameEndpoint>(provider, "Hello:World");

        var ex = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<EndpointMcpToolSource>().BuildTools());

        ex.Message.ShouldContain("Duplicate MCP tool names detected");
        ex.Message.ShouldContain("'hello_world'");
        ex.Message.ShouldContain(typeof(FirstDuplicateGeneratedNameEndpoint).FullName!);
        ex.Message.ShouldContain(typeof(SecondDuplicateGeneratedNameEndpoint).FullName!);
    }

    [Fact]
    public async Task ListTools_returns_normalized_generated_name()
    {
        using var provider = BuildServices(typeof(SummaryDerivedNameEndpoint));
        ConfigureGeneratedTool<SummaryDerivedNameEndpoint>(provider, "Get user (admin-only)");
        SetUser(provider);

        var result = await ListTools(provider);

        result.Tools.Select(x => x.Name).ShouldBe(["get_user_admin_only"]);
    }

    [Fact]
    public async Task CallTool_succeeds_using_normalized_generated_name()
    {
        using var provider = BuildServices(typeof(SummaryDerivedNameEndpoint));
        ConfigureGeneratedTool<SummaryDerivedNameEndpoint>(provider, "Get user (admin-only)");
        SetUser(provider);

        var result = await CallTool(provider, "get_user_admin_only");

        ((TextContentBlock)result.Content[0]).Text.ShouldContain("summary:ping");
    }

    static ServiceProvider BuildServices(params Type[] endpointTypes)
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
                foreach (var endpointType in endpointTypes)
                    o.SourceGeneratorDiscoveredTypes.Add(endpointType);
            });
        services.AddMcp(o => o.ToolVisibilityFilter = static (_, _, _) => true);

        return services.BuildServiceProvider();
    }

    static EndpointDefinition GetDefinition<TEndpoint>(IServiceProvider provider)
        => provider.GetRequiredService<EndpointData>().Found.Single(x => x.EndpointType == typeof(TEndpoint));

    static void ConfigureGeneratedTool<TEndpoint>(IServiceProvider provider, string summary)
    {
        var def = GetDefinition<TEndpoint>(provider);
        def.McpTool();
        def.Summary(s => s.Summary = summary);
    }

    static async Task<ListToolsResult> ListTools(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;

        return await options.Handlers.ListToolsHandler!(McpToolVisibilityTests_Bridge.BuildListRequestContext(provider, BuildPrincipal()), CancellationToken.None);
    }

    static async Task<CallToolResult> CallTool(IServiceProvider provider, string toolName)
    {
        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;

        return await options.Handlers.CallToolHandler!(McpToolVisibilityTests_Bridge.BuildCallRequestContext(provider, toolName, BuildPrincipal(), BuildArguments()), CancellationToken.None);
    }

    static Dictionary<string, JsonElement> BuildArguments()
        => new()
        {
            ["Value"] = JsonSerializer.SerializeToElement("ping")
        };

    static ClaimsPrincipal BuildPrincipal()
        => new(new ClaimsIdentity([new("sub", "caller")], "test"));

    static void SetUser(IServiceProvider provider)
    {
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            RequestServices = provider,
            User = BuildPrincipal()
        };
    }

    [HttpPost("/explicit-invalid-name")]
    sealed class ExplicitInvalidNameEndpoint : Endpoint<ToolRequest, ToolResponse>
    {
        public override Task HandleAsync(ToolRequest req, CancellationToken ct)
            => Send.OkAsync(new() { Value = "explicit:" + req.Value }, ct);
    }

    [HttpPost("/summary-derived-name")]
    sealed class SummaryDerivedNameEndpoint : Endpoint<ToolRequest, ToolResponse>
    {
        public override Task HandleAsync(ToolRequest req, CancellationToken ct)
            => Send.OkAsync(new() { Value = "summary:" + req.Value }, ct);
    }

    [McpTool("bad.name")]
    [HttpPost("/attribute-invalid-name")]
    sealed class AttributeInvalidNameEndpoint : Endpoint<ToolRequest, ToolResponse>
    {
        public override Task HandleAsync(ToolRequest req, CancellationToken ct)
            => Send.OkAsync(new() { Value = "attribute-invalid:" + req.Value }, ct);
    }

    [HttpPost("/empty-generated-name")]
    sealed class EmptyGeneratedNameEndpoint : Endpoint<ToolRequest, ToolResponse>
    {
        public override Task HandleAsync(ToolRequest req, CancellationToken ct)
            => Send.OkAsync(new() { Value = "empty:" + req.Value }, ct);
    }

    [HttpPost("/first-duplicate-generated-name")]
    sealed class FirstDuplicateGeneratedNameEndpoint : Endpoint<ToolRequest, ToolResponse>
    {
        public override Task HandleAsync(ToolRequest req, CancellationToken ct)
            => Send.OkAsync(new() { Value = "first:" + req.Value }, ct);
    }

    [HttpPost("/second-duplicate-generated-name")]
    sealed class SecondDuplicateGeneratedNameEndpoint : Endpoint<ToolRequest, ToolResponse>
    {
        public override Task HandleAsync(ToolRequest req, CancellationToken ct)
            => Send.OkAsync(new() { Value = "second:" + req.Value }, ct);
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
