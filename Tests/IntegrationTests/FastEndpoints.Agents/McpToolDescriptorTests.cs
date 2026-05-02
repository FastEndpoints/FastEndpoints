using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using FastEndpoints.Mcp;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FastEndpoints.Agents.Tests;

public class McpToolDescriptorTests
{
    [Fact]
    public void Fluent_tool_metadata_populates_protocol_descriptor()
    {
        using var provider = BuildServices();

        var tool = BuildTool(provider, "descriptor_tool");
        var protocolTool = tool.ProtocolTool;

        protocolTool.Name.ShouldBe("descriptor_tool");
        protocolTool.Description.ShouldBe("Reads structured endpoint data.");
        protocolTool.Title.ShouldBe("Descriptor Tool");
        protocolTool.InputSchema.ValueKind.ShouldBe(JsonValueKind.Object);
        protocolTool.InputSchema.GetProperty("type").GetString().ShouldBe("object");
        protocolTool.InputSchema.GetProperty("properties").GetProperty("Value").ValueKind.ShouldBe(JsonValueKind.Object);
        protocolTool.OutputSchema.HasValue.ShouldBeTrue();
        protocolTool.OutputSchema.Value.ValueKind.ShouldBe(JsonValueKind.Object);
        protocolTool.OutputSchema.Value.GetProperty("type").GetString().ShouldBe("object");
        protocolTool.OutputSchema.Value.GetProperty("properties").GetProperty("Value").ValueKind.ShouldBe(JsonValueKind.Object);
        protocolTool.Annotations.ShouldNotBeNull();
        protocolTool.Annotations.ReadOnlyHint.ShouldBe(true);
        protocolTool.Annotations.IdempotentHint.ShouldBe(true);
        protocolTool.Annotations.DestructiveHint.ShouldBe(false);
        protocolTool.Annotations.OpenWorldHint.ShouldBe(false);
    }

    [Fact]
    public void Output_schema_is_omitted_when_disabled()
    {
        using var provider = BuildServices(o => o.IncludeOutputSchemas = false);

        var tool = BuildTool(provider, "descriptor_tool");

        tool.ProtocolTool.InputSchema.ValueKind.ShouldBe(JsonValueKind.Object);
        tool.ProtocolTool.OutputSchema.HasValue.ShouldBeFalse();
    }

    [Fact]
    public void Attribute_tool_metadata_populates_protocol_descriptor()
    {
        using var provider = BuildServices();

        var tool = BuildTool(provider, "attribute_tool");
        var protocolTool = tool.ProtocolTool;

        protocolTool.Title.ShouldBe("Attribute Tool");
        protocolTool.Description.ShouldBe("Writes to an external system.");
        protocolTool.Annotations.ShouldNotBeNull();
        protocolTool.Annotations.ReadOnlyHint.ShouldBe(true);
        protocolTool.Annotations.IdempotentHint.ShouldBe(true);
        protocolTool.Annotations.DestructiveHint.ShouldBe(true);
        protocolTool.Annotations.OpenWorldHint.ShouldBe(true);
    }

    [Fact]
    public async Task Structured_content_is_populated_for_json_object_response_when_output_schema_is_enabled()
    {
        using var provider = BuildServices();
        SetUser(provider, true);

        var tool = BuildTool(provider, "descriptor_tool");
        var result = await tool.InvokeAsync(BuildRequestContext(provider, tool, authenticated: true), CancellationToken.None);

        result.StructuredContent.HasValue.ShouldBeTrue();
        result.StructuredContent.Value.ValueKind.ShouldBe(JsonValueKind.Object);
        result.StructuredContent.Value.GetProperty("Value").GetString().ShouldBe("visible:ping");
        ((TextContentBlock)result.Content[0]).Text.ShouldContain("visible:ping");
    }

    static ServiceProvider BuildServices(Action<McpOptions>? configure = null)
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
                o.SourceGeneratorDiscoveredTypes.Add(typeof(DescriptorToolEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(AttributeToolEndpoint));
            });
        services.AddMcp(
            o =>
            {
                o.ToolVisibilityFilter = static (_, _, _) => true;
                configure?.Invoke(o);
            });

        var provider = services.BuildServiceProvider();

        foreach (var def in provider.GetRequiredService<EndpointData>().Found)
        {
            if (def.EndpointType == typeof(DescriptorToolEndpoint))
            {
                def.McpTool(
                    "descriptor_tool",
                    "Reads structured endpoint data.",
                    info =>
                    {
                        info.Title = "Descriptor Tool";
                        info.Hints.ReadOnly = true;
                        info.Hints.Idempotent = true;
                        info.Hints.Destructive = false;
                        info.Hints.OpenWorld = false;
                    });
            }
        }

        return provider;
    }

    static McpServerTool BuildTool(IServiceProvider provider, string name)
        => provider.GetRequiredService<EndpointMcpToolSource>().BuildTools().Single(t => t.ProtocolTool.Name == name);

    static RequestContext<CallToolRequestParams> BuildRequestContext(IServiceProvider provider, McpServerTool tool, bool authenticated)
    {
        var user = authenticated
                       ? new ClaimsPrincipal(new ClaimsIdentity([new("sub", "caller")], "test"))
                       : new ClaimsPrincipal(new ClaimsIdentity());

        return McpToolVisibilityTests_Bridge.BuildRequestContext(provider, tool, user);
    }

    static void SetUser(IServiceProvider provider, bool authenticated)
    {
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            RequestServices = provider,
            User = authenticated
                       ? new ClaimsPrincipal(new ClaimsIdentity([new("sub", "caller")], "test"))
                       : new ClaimsPrincipal(new ClaimsIdentity())
        };
    }

    [HttpPost("/descriptor-tool")]
    sealed class DescriptorToolEndpoint : Endpoint<ToolRequest, ToolResponse>
    {
        public override async Task HandleAsync(ToolRequest req, CancellationToken ct)
            => await Send.OkAsync(new() { Value = "visible:" + req.Value }, ct);
    }

    [McpTool("attribute_tool", Description = "Writes to an external system.", Title = "Attribute Tool", ReadOnly = true, Idempotent = true, Destructive = true, OpenWorld = true)]
    [HttpPost("/attribute-tool")]
    sealed class AttributeToolEndpoint : Endpoint<ToolRequest, ToolResponse>
    {
        public override async Task HandleAsync(ToolRequest req, CancellationToken ct)
            => await Send.OkAsync(new() { Value = "attribute:" + req.Value }, ct);
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

static class McpToolVisibilityTests_Bridge
{
    public static RequestContext<CallToolRequestParams> BuildRequestContext(IServiceProvider provider, McpServerTool tool, ClaimsPrincipal user)
    {
        var request = new JsonRpcRequest
        {
            Id = new RequestId(1),
            Method = RequestMethods.ToolsCall
        };
        var server = McpServer.Create(new TestTransport(), new McpServerOptions(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, provider);

        return new(
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
    }

    sealed class TestTransport : ITransport
    {
        readonly System.Threading.Channels.Channel<JsonRpcMessage> _messages = System.Threading.Channels.Channel.CreateUnbounded<JsonRpcMessage>();

        public string? SessionId => null;

        public System.Threading.Channels.ChannelReader<JsonRpcMessage> MessageReader => _messages.Reader;

        public ValueTask DisposeAsync()
        {
            _messages.Writer.TryComplete();

            return ValueTask.CompletedTask;
        }

        public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
