using System.Security.Claims;
using System.Text.Json;
using FastEndpoints.Mcp;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FastEndpoints.Agents.Tests;

public class McpToolSchemaRootTests
{
    [Fact]
    public async Task Object_request_string_response_uses_text_only_results()
    {
        using var provider = BuildServices(
            typeof(ObjectToStringEndpoint),
            typeof(ObjectToArrayEndpoint),
            typeof(ObjectToObjectEndpoint));
        SetUser(provider, true);

        var tool = BuildTool(provider, "object_to_string");
        var result = await tool.InvokeAsync(BuildRequestContext(provider, tool, BuildObjectArguments()), CancellationToken.None);

        tool.ProtocolTool.InputSchema.GetProperty("type").GetString().ShouldBe("object");
        tool.ProtocolTool.OutputSchema.HasValue.ShouldBeFalse();
        result.StructuredContent.HasValue.ShouldBeFalse();
        ((TextContentBlock)result.Content[0]).Text.ShouldBe("\"value:ping\"");
    }

    [Fact]
    public async Task Object_request_array_response_uses_text_only_results()
    {
        using var provider = BuildServices(
            typeof(ObjectToStringEndpoint),
            typeof(ObjectToArrayEndpoint),
            typeof(ObjectToObjectEndpoint));
        SetUser(provider, true);

        var tool = BuildTool(provider, "object_to_array");
        var result = await tool.InvokeAsync(BuildRequestContext(provider, tool, BuildObjectArguments()), CancellationToken.None);

        tool.ProtocolTool.InputSchema.GetProperty("type").GetString().ShouldBe("object");
        tool.ProtocolTool.OutputSchema.HasValue.ShouldBeFalse();
        result.StructuredContent.HasValue.ShouldBeFalse();
        ((TextContentBlock)result.Content[0]).Text.ShouldBe("[\"value:ping\"]");
    }

    [Theory]
    [InlineData(typeof(ArrayRequestEndpoint), "array_request")]
    [InlineData(typeof(StringRequestEndpoint), "string_request")]
    public void Scalar_or_array_request_schema_is_rejected(Type endpointType, string toolName)
    {
        using var provider = BuildServices(endpointType);
        var source = provider.GetRequiredService<EndpointMcpToolSource>();

        var ex = Should.Throw<InvalidOperationException>(() => source.BuildTools());

        ex.Message.ShouldContain($"MCP tool '{toolName}' cannot use input schema generated from");
        ex.Message.ShouldContain("MCP tools require an object root schema");
        ex.Message.ShouldContain("object-shaped DTO for tool arguments");
    }

    [Fact]
    public async Task Object_request_object_response_keeps_structured_content()
    {
        using var provider = BuildServices(
            typeof(ObjectToStringEndpoint),
            typeof(ObjectToArrayEndpoint),
            typeof(ObjectToObjectEndpoint));
        SetUser(provider, true);

        var tool = BuildTool(provider, "object_to_object");
        var result = await tool.InvokeAsync(BuildRequestContext(provider, tool, BuildObjectArguments()), CancellationToken.None);

        tool.ProtocolTool.OutputSchema.HasValue.ShouldBeTrue();
        tool.ProtocolTool.OutputSchema.Value.GetProperty("type").GetString().ShouldBe("object");
        result.StructuredContent.HasValue.ShouldBeTrue();
        result.StructuredContent.Value.GetProperty("Value").GetString().ShouldBe("value:ping");
        ((TextContentBlock)result.Content[0]).Text.ShouldContain("value:ping");
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

        var provider = services.BuildServiceProvider();

        foreach (var def in provider.GetRequiredService<EndpointData>().Found)
        {
            if (def.EndpointType == typeof(ObjectToStringEndpoint))
                def.McpTool("object_to_string");

            if (def.EndpointType == typeof(ObjectToArrayEndpoint))
                def.McpTool("object_to_array");

            if (def.EndpointType == typeof(ObjectToObjectEndpoint))
                def.McpTool("object_to_object");

            if (def.EndpointType == typeof(ArrayRequestEndpoint))
                def.McpTool("array_request");

            if (def.EndpointType == typeof(StringRequestEndpoint))
                def.McpTool("string_request");
        }

        return provider;
    }

    static McpServerTool BuildTool(IServiceProvider provider, string name)
        => provider.GetRequiredService<EndpointMcpToolSource>().BuildTools().Single(t => t.ProtocolTool.Name == name);

    static RequestContext<CallToolRequestParams> BuildRequestContext(IServiceProvider provider, McpServerTool tool, Dictionary<string, JsonElement> arguments)
        => McpToolVisibilityTests_Bridge.BuildCallRequestContext(provider, tool.ProtocolTool.Name, BuildPrincipal(true), arguments, tool);

    static Dictionary<string, JsonElement> BuildObjectArguments()
        => new() { ["Value"] = JsonSerializer.SerializeToElement("ping") };

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

    [HttpPost("/mcp-schema-root/object-string")]
    sealed class ObjectToStringEndpoint : Endpoint<ObjectRequest, string>
    {
        public override Task HandleAsync(ObjectRequest req, CancellationToken ct)
            => Send.OkAsync("value:" + req.Value, ct);
    }

    [HttpPost("/mcp-schema-root/object-array")]
    sealed class ObjectToArrayEndpoint : Endpoint<ObjectRequest, string[]>
    {
        public override Task HandleAsync(ObjectRequest req, CancellationToken ct)
            => Send.OkAsync(["value:" + req.Value], ct);
    }

    [HttpPost("/mcp-schema-root/object-object")]
    sealed class ObjectToObjectEndpoint : Endpoint<ObjectRequest, ObjectResponse>
    {
        public override Task HandleAsync(ObjectRequest req, CancellationToken ct)
            => Send.OkAsync(new() { Value = "value:" + req.Value }, ct);
    }

    [HttpPost("/mcp-schema-root/array-request")]
    sealed class ArrayRequestEndpoint : Endpoint<ObjectRequest[], ObjectResponse>
    {
        public override Task HandleAsync(ObjectRequest[] req, CancellationToken ct)
            => Send.OkAsync(new() { Value = req.Length.ToString() }, ct);
    }

    [HttpPost("/mcp-schema-root/string-request")]
    sealed class StringRequestEndpoint : Endpoint<string, ObjectResponse>
    {
        public override Task HandleAsync(string req, CancellationToken ct)
            => Send.OkAsync(new() { Value = req }, ct);
    }

    sealed class ObjectRequest
    {
        public string Value { get; set; } = "";
    }

    sealed class ObjectResponse
    {
        public string? Value { get; set; }
    }
}
