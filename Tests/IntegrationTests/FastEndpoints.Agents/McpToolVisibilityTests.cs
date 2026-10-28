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
    [Theory]
    [InlineData("resolution", true)]
    [InlineData("resolution", false)]
    [InlineData("direct invocation", true)]
    [InlineData("direct invocation", false)]
    [InlineData("invocation recheck", true)]
    public async Task Refused_call_reports_endpoint_and_request_caller_once(string path, bool authenticated)
    {
        var checks = 0;
        using var provider = BuildServices((_, _, _) => ++checks == 1 && path == "invocation recheck");
        SetUser(provider, authenticated);
        var httpContext = provider.GetRequiredService<IHttpContextAccessor>().HttpContext!;
        var tool = BuildTool(provider);
        var ctx = BuildRequestContext(provider, tool, authenticated);
        var refusals = new List<(EndpointDefinition Definition, ClaimsPrincipal Caller, HttpContext Context)>();
        provider.GetRequiredService<McpOptions>().OnToolCallRefused = (def, caller, http) => refusals.Add((def, caller, http));

        var ex = await Should.ThrowAsync<McpException>(async () =>
        {
            if (path == "direct invocation")
                await tool.InvokeAsync(ctx, CancellationToken.None);
            else
                await provider.GetRequiredService<IOptions<McpServerOptions>>().Value.Handlers.CallToolHandler!(ctx, CancellationToken.None);
        });

        ex.Message.ShouldBe("Tool 'visible' is not available for the current caller.");
        var refusal = refusals.ShouldHaveSingleItem();
        refusal.Definition.ShouldBeSameAs(provider.GetRequiredService<EndpointData>().Found.Single());
        refusal.Caller.ShouldBeSameAs(ctx.User);
        refusal.Context.ShouldBeSameAs(httpContext);
        checks.ShouldBe(path == "invocation recheck" ? 2 : 1);
    }

    [Fact]
    public async Task Listing_hidden_tools_does_not_report_refused_calls()
    {
        using var provider = BuildServices((_, _, _) => false);
        SetUser(provider, authenticated: true);
        var refusals = 0;
        provider.GetRequiredService<McpOptions>().OnToolCallRefused = (_, _, _) => refusals++;

        (await ListTools(provider, authenticated: true)).Tools.ShouldBeEmpty();
        (await ListTools(provider, authenticated: true)).Tools.ShouldBeEmpty();

        refusals.ShouldBe(0);
    }

    [Fact]
    public async Task Allowed_call_does_not_report_refusal()
    {
        using var provider = BuildServices();
        SetUser(provider, authenticated: true);
        var refusals = 0;
        provider.GetRequiredService<McpOptions>().OnToolCallRefused = (_, _, _) => refusals++;

        await CallTool(provider, "visible", authenticated: true);

        refusals.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_or_statically_excluded_tool_does_not_report_refusal(bool excludeTool)
    {
        using var provider = BuildServices((_, _, _) => false);
        SetUser(provider, authenticated: true);
        var refusals = 0;
        var options = provider.GetRequiredService<McpOptions>();
        options.OnToolCallRefused = (_, _, _) => refusals++;
        if (excludeTool)
            options.ToolFilter = _ => false;
        var name = excludeTool ? "visible" : "unknown";

        var ex = await Should.ThrowAsync<McpException>(async () => await CallTool(provider, name, authenticated: true));

        ex.Message.ShouldBe($"Tool '{name}' is not available for the current caller.");
        refusals.ShouldBe(0);
    }

    [Fact]
    public async Task Ambient_caller_is_reported_when_request_context_user_is_missing()
    {
        using var provider = BuildServices((_, _, _) => false);
        var principal = SetUser(provider, authenticated: true);
        ClaimsPrincipal? reportedCaller = null;
        provider.GetRequiredService<McpOptions>().OnToolCallRefused = (_, caller, _) => reportedCaller = caller;

        await Should.ThrowAsync<McpException>(async () => await CallToolWithoutRequestContextUser(provider, "visible"));

        reportedCaller.ShouldBeSameAs(principal);
    }

    [Fact]
    public async Task Callback_exceptions_propagate_without_invoking_the_endpoint()
    {
        using var provider = BuildServices((_, _, _) => false);
        SetUser(provider, authenticated: true);
        var failure = new InvalidOperationException("Audit failed.");
        provider.GetRequiredService<McpOptions>().OnToolCallRefused = (_, _, _) => throw failure;

        var ex = await Should.ThrowAsync<InvalidOperationException>(async () => await CallTool(provider, "visible", authenticated: true));

        ex.ShouldBeSameAs(failure);
    }

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

    [Fact]
    public async Task Ambient_authenticated_http_context_user_can_list_tools_when_request_context_user_is_missing()
    {
        using var provider = BuildServices();
        var principal = SetUser(provider, authenticated: true);

        var result = await ListToolsWithoutRequestContextUser(provider);

        result.Tools.Select(t => t.Name).ShouldBe(["visible"]);
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext!.User.ShouldBeSameAs(principal);
    }

    [Fact]
    public async Task Ambient_authenticated_http_context_user_can_call_tool_when_request_context_user_is_missing()
    {
        using var provider = BuildServices();
        SetUser(provider, authenticated: true);

        var result = await CallToolWithoutRequestContextUser(provider, "visible");

        ((TextContentBlock)result.Content[0]).Text.ShouldContain("visible:ping");
    }

    [Fact]
    public void Refusal_auditing_can_be_enabled_and_disabled_between_resolutions()
    {
        using var provider = BuildServices((_, _, _) => false);
        SetUser(provider, authenticated: true);
        var source = provider.GetRequiredService<EndpointMcpToolSource>();
        var options = provider.GetRequiredService<McpOptions>();
        var refusals = 0;

        source.ResolveVisibleTool("visible").ShouldBeNull();
        options.OnToolCallRefused = (_, _, _) => refusals++;
        source.ResolveVisibleTool("visible").ShouldBeNull();
        refusals.ShouldBe(1);

        options.OnToolCallRefused = null;
        source.ResolveVisibleTool("visible").ShouldBeNull();
        refusals.ShouldBe(1);
    }

    [Fact]
    public void Enabling_auditing_inside_visibility_filter_applies_to_next_resolution()
    {
        McpOptions options = null!;
        var refusals = 0;
        using var provider = BuildServices((_, _, _) =>
        {
            options.OnToolCallRefused = (_, _, _) => refusals++;
            return false;
        });
        options = provider.GetRequiredService<McpOptions>();
        SetUser(provider, authenticated: true);
        var source = provider.GetRequiredService<EndpointMcpToolSource>();

        source.ResolveVisibleTool("visible").ShouldBeNull();
        refusals.ShouldBe(0);
        source.ResolveVisibleTool("visible").ShouldBeNull();
        refusals.ShouldBe(1);
    }

    [Theory]
    [InlineData("visible")]
    [InlineData("unknown")]
    public void Non_audited_resolution_allocates_less_than_audited_resolution(string name)
    {
        using var provider = BuildServices();
        SetUser(provider, authenticated: true);
        var source = provider.GetRequiredService<EndpointMcpToolSource>();
        var options = provider.GetRequiredService<McpOptions>();
        var context = provider.GetRequiredService<IHttpContextAccessor>().HttpContext!;
        const int iterations = 10_000;

        var withoutAudit = Measure(false);
        var withAudit = Measure(true);

        TestContext.Current.TestOutputHelper!.WriteLine($"{name}: disabled={withoutAudit / iterations} B/call, enabled={withAudit / iterations} B/call");
        withoutAudit.ShouldBeLessThan(withAudit);

        long Measure(bool audit)
        {
            options.OnToolCallRefused = audit ? static (_, _, _) => { } : null;

            for (var i = 0; i < iterations; i++)
                source.ResolveVisibleTool(name, context.User, context);

            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var i = 0; i < iterations; i++)
                source.ResolveVisibleTool(name, context.User, context);

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
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
        services.AddFastEndpoints([typeof(VisibleToolEndpoint)]);
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

        return await handler(McpToolVisibilityTestsBridge.BuildListRequestContext(provider, BuildPrincipal(authenticated)), CancellationToken.None);
    }

    static async Task CallTool(IServiceProvider provider, string toolName, bool authenticated)
    {
        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var handler = options.Handlers.CallToolHandler;
        handler.ShouldNotBeNull();

        await handler(McpToolVisibilityTestsBridge.BuildCallRequestContext(provider, toolName, BuildPrincipal(authenticated)), CancellationToken.None);
    }

    static async Task<ListToolsResult> ListToolsWithoutRequestContextUser(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var ctx = McpToolVisibilityTestsBridge.BuildListRequestContext(provider, BuildPrincipal(authenticated: false));
        ctx.User = null!;

        return await options.Handlers.ListToolsHandler!(ctx, CancellationToken.None);
    }

    static async Task<CallToolResult> CallToolWithoutRequestContextUser(IServiceProvider provider, string toolName)
    {
        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var ctx = McpToolVisibilityTestsBridge.BuildCallRequestContext(provider, toolName, BuildPrincipal(authenticated: false));
        ctx.User = null!;

        return await options.Handlers.CallToolHandler!(ctx, CancellationToken.None);
    }

    static RequestContext<CallToolRequestParams> BuildRequestContext(IServiceProvider provider, McpServerTool tool, bool authenticated)
    {
        var user = BuildPrincipal(authenticated);
        var request = new JsonRpcRequest
        {
            Id = new(1),
            Method = RequestMethods.ToolsCall
        };
        var server = McpServer.Create(new TestTransport(), new(), NullLoggerFactory.Instance, provider);
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

    static ClaimsPrincipal SetUser(IServiceProvider provider, bool authenticated)
    {
        var principal = BuildPrincipal(authenticated);

        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            RequestServices = provider,
            User = principal
        };

        return principal;
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

    // ReSharper disable once ClassNeverInstantiated.Local
    sealed class ToolRequest
    {
        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
        public string Value { get; set; } = "";
    }

    sealed class ToolResponse
    {
        public string? Value { get; set; }
    }
}
