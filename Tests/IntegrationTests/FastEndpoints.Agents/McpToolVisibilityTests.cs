using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using FastEndpoints.Mcp;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FastEndpoints.Agents.Tests;

public class McpToolVisibilityTests
{
    [Fact]
    public async Task Anonymous_caller_cannot_invoke_opt_in_tool_by_default()
    {
        using var provider = BuildServices();
        SetUser(provider, authenticated: false);

        var tool = BuildTool(provider);
        var ctx = BuildRequestContext(provider, tool, authenticated: false);

        var ex = await Should.ThrowAsync<McpException>(async () => await tool.InvokeAsync(ctx, CancellationToken.None));

        ex.Message.ShouldBe("Tool 'visible' is not available for the current caller.");
    }

    [Fact]
    public async Task Anonymous_caller_cannot_list_opt_in_tool_by_default()
    {
        using var provider = BuildServices();
        SetUser(provider, authenticated: false);

        var result = await ListTools(provider, authenticated: false);

        result.Tools.ShouldBeEmpty();
    }

    [Fact]
    public async Task Authenticated_caller_can_invoke_opt_in_tool_by_default()
    {
        using var provider = BuildServices();
        SetUser(provider, authenticated: true);

        var tool = BuildTool(provider);
        var ctx = BuildRequestContext(provider, tool, authenticated: true);

        var result = await tool.InvokeAsync(ctx, CancellationToken.None);

        ((TextContentBlock)result.Content[0]).Text.ShouldContain("visible:ping");
    }

    [Fact]
    public async Task Authenticated_caller_can_list_opt_in_tool_by_default()
    {
        using var provider = BuildServices();
        SetUser(provider, authenticated: true);

        var result = await ListTools(provider, authenticated: true);

        result.Tools.Select(t => t.Name).ShouldBe(["visible"]);
    }

    [Fact]
    public async Task Always_true_visibility_filter_restores_anonymous_tool_access()
    {
        using var provider = BuildServices((_, _, _) => true);
        SetUser(provider, authenticated: false);

        var tool = BuildTool(provider);
        var ctx = BuildRequestContext(provider, tool, authenticated: false);

        var result = await tool.InvokeAsync(ctx, CancellationToken.None);

        ((TextContentBlock)result.Content[0]).Text.ShouldContain("visible:ping");
    }

    [Fact]
    public async Task Always_true_visibility_filter_restores_anonymous_tool_listing()
    {
        using var provider = BuildServices((_, _, _) => true);
        SetUser(provider, authenticated: false);

        var result = await ListTools(provider, authenticated: false);

        result.Tools.Select(t => t.Name).ShouldBe(["visible"]);
    }

    [Fact]
    public async Task Hidden_tools_remain_blocked_on_direct_invoke_through_server_handler()
    {
        using var provider = BuildServices();
        SetUser(provider, authenticated: false);

        var ex = await Should.ThrowAsync<McpException>(async () => await CallTool(provider, "visible", authenticated: false));

        ex.Message.ShouldBe("Tool 'visible' is not available for the current caller.");
    }

    static ServiceProvider BuildServices(Func<EndpointDefinition, ClaimsPrincipal, HttpContext, bool>? visibilityFilter = null)
    {
        Factory.RegisterTestServices(
            s =>
            {
                s.AddSingleton(typeof(IRequestBinder<>), typeof(RequestBinder<>));
            });

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddFastEndpoints(o => o.SourceGeneratorDiscoveredTypes.Add(typeof(VisibleToolEndpoint)));
        services.AddMcp(
            o =>
            {
                if (visibilityFilter is not null)
                    o.ToolVisibilityFilter = visibilityFilter;
            });

        var provider = services.BuildServiceProvider();

        foreach (var def in provider.GetRequiredService<EndpointData>().Found)
        {
            if (def.EndpointType == typeof(VisibleToolEndpoint))
                def.McpTool("visible");
        }

        return provider;
    }

    static McpServerTool BuildTool(IServiceProvider provider)
        => provider.GetRequiredService<EndpointMcpToolSource>().BuildTools().Single();

    static async Task<ListToolsResult> ListTools(IServiceProvider provider, bool authenticated)
    {
        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var handler = options.Handlers.ListToolsHandler;
        handler.ShouldNotBeNull();

        return await handler!(McpToolVisibilityTests_Bridge.BuildListRequestContext(provider, BuildPrincipal(authenticated)), CancellationToken.None);
    }

    static async Task<CallToolResult> CallTool(IServiceProvider provider, string toolName, bool authenticated)
    {
        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var handler = options.Handlers.CallToolHandler;
        handler.ShouldNotBeNull();

        return await handler!(McpToolVisibilityTests_Bridge.BuildCallRequestContext(provider, toolName, BuildPrincipal(authenticated)), CancellationToken.None);
    }

    static RequestContext<CallToolRequestParams> BuildRequestContext(IServiceProvider provider, McpServerTool tool, bool authenticated)
    {
        var user = BuildPrincipal(authenticated);
        var request = new JsonRpcRequest
        {
            Id = new RequestId(1),
            Method = RequestMethods.ToolsCall
        };
        var server = McpServer.Create(new TestTransport(), new McpServerOptions(), NullLoggerFactory.Instance, provider);
        var ctx = new RequestContext<CallToolRequestParams>(
            server,
            request,
            new()
            {
                Name = tool.ProtocolTool.Name,
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["Value"] = JsonSerializer.SerializeToElement("ping")
                }
            })
        {
            Services = provider,
            User = user,
            MatchedPrimitive = tool
        };

        return ctx;
    }

    static void SetUser(IServiceProvider provider, bool authenticated)
    {
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            RequestServices = provider,
            User = BuildPrincipal(authenticated)
        };
    }

    static ClaimsPrincipal BuildPrincipal(bool authenticated)
        => authenticated
               ? new(new ClaimsIdentity([new("sub", "caller")], "test"))
               : new(new ClaimsIdentity());

    sealed class TestTransport : ITransport
    {
        readonly Channel<JsonRpcMessage> _messages = Channel.CreateUnbounded<JsonRpcMessage>();

        public string? SessionId => null;

        public ChannelReader<JsonRpcMessage> MessageReader => _messages.Reader;

        public ValueTask DisposeAsync()
        {
            _messages.Writer.TryComplete();

            return ValueTask.CompletedTask;
        }

        public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    [HttpPost("/visible-tool")]
    sealed class VisibleToolEndpoint : Endpoint<ToolRequest, ToolResponse>
    {
        public override async Task HandleAsync(ToolRequest req, CancellationToken ct)
            => await Send.OkAsync(new() { Value = "visible:" + req.Value }, ct);
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
