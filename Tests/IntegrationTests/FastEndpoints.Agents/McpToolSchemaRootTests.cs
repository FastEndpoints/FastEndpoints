using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public async Task No_request_endpoint_uses_empty_object_input_schema()
    {
        using var provider = BuildServices(typeof(NoRequestEndpoint));
        SetUser(provider, true);

        var tool = BuildTool(provider, "no_request");
        var result = await tool.InvokeAsync(BuildRequestContext(provider, tool, []), CancellationToken.None);

        tool.ProtocolTool.InputSchema.GetProperty("type").GetString().ShouldBe("object");
        tool.ProtocolTool.InputSchema.GetProperty("properties").EnumerateObject().ShouldBeEmpty();
        result.StructuredContent.HasValue.ShouldBeTrue();
        result.StructuredContent.Value.GetProperty("Value").GetString().ShouldBe("no-request");
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

    [Theory, InlineData(typeof(ArrayRequestEndpoint), "array_request"), InlineData(typeof(StringRequestEndpoint), "string_request")]
    public void Scalar_or_array_request_schema_is_rejected(Type endpointType, string toolName)
    {
        using var provider = BuildServices(endpointType);
        var source = provider.GetRequiredService<EndpointMcpToolSource>();

        var ex = Should.Throw<InvalidOperationException>(source.BuildTools);

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
        tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("Value").GetProperty("description").GetString().ShouldBe("Value to echo");
        tool.ProtocolTool.OutputSchema.Value.GetProperty("properties").GetProperty("Value").GetProperty("description").GetString().ShouldBe("Echoed value");
        result.StructuredContent.HasValue.ShouldBeTrue();
        result.StructuredContent.Value.GetProperty("Value").GetString().ShouldBe("value:ping");
        ((TextContentBlock)result.Content[0]).Text.ShouldContain("value:ping");
    }

    [Fact]
    public async Task Duplicate_response_properties_keep_first_position_and_last_value()
    {
        using var provider = BuildServices(typeof(DuplicatePropertiesEndpoint));
        SetUser(provider, true);

        var tool = BuildTool(provider, "duplicate_properties");
        var result = await tool.InvokeAsync(BuildRequestContext(provider, tool, []), CancellationToken.None);

        tool.ProtocolTool.OutputSchema.HasValue.ShouldBeTrue();
        result.StructuredContent.HasValue.ShouldBeTrue();
        result.StructuredContent.Value.GetRawText().ShouldBe("""{"id":2,"other":true}""");
    }

    [Theory, InlineData(true, false), InlineData(false, true), InlineData(false, false)]
    public void Pass_through_content_owns_its_json_after_mapping(bool objectSchema, bool includePropertyNames)
    {
        var body = Encoding.UTF8.GetBytes("""{"value":{"items":[1,2]},"extra":true}""");
        var schema = JsonNode.Parse(objectSchema ? """{"type":"object"}""" : "true")!;
        HashSet<string>? names = includePropertyNames ? new(StringComparer.Ordinal) { "value" } : null;

        McpStructuredContentMapper.TryMap(body, schema, names, out var content).ShouldBeTrue();
        Array.Clear(body);

        content.GetProperty("value").GetProperty("items")[1].GetInt32().ShouldBe(2);
        JsonSerializer.Serialize(content).ShouldBe("""{"value":{"items":[1,2]},"extra":true}""");
    }

    [Fact]
    public void Filtered_content_owns_its_json_after_mapping()
    {
        var body = Encoding.UTF8.GetBytes("""{"value":{"items":[1,2]},"extra":true}""");
        var schema = JsonNode.Parse("""{"type":"object","properties":{"value":{"type":"object","properties":{"items":{"type":"array","items":{"type":"integer"}}}}}}""")!;

        McpStructuredContentMapper.TryMap(body, schema, new(StringComparer.Ordinal) { "value" }, out var content).ShouldBeTrue();
        Array.Clear(body);

        content.GetProperty("value").GetProperty("items")[1].GetInt32().ShouldBe(2);
        content.TryGetProperty("extra", out _).ShouldBeFalse();
        JsonSerializer.Serialize(content).ShouldBe("""{"value":{"items":[1,2]}}""");
    }

    [Theory, InlineData(""), InlineData(" "), InlineData("{"), InlineData("null"), InlineData("[]"), InlineData("42"), InlineData("\"text\"")]
    public void Invalid_or_non_object_content_is_not_mapped(string json)
    {
        var schema = JsonNode.Parse("""{"type":"object"}""")!;

        McpStructuredContentMapper.TryMap(Encoding.UTF8.GetBytes(json), schema, null, out var content).ShouldBeFalse();

        content.ValueKind.ShouldBe(JsonValueKind.Undefined);
    }

    [Fact]
    public void Content_exceeding_the_json_depth_limit_is_not_mapped()
    {
        var body = Encoding.UTF8.GetBytes("{\"value\":" + new string('[', 64) + "0" + new string(']', 64) + "}");
        var schema = JsonNode.Parse("""{"type":"object"}""")!;

        McpStructuredContentMapper.TryMap(body, schema, null, out var content).ShouldBeFalse();

        content.ValueKind.ShouldBe(JsonValueKind.Undefined);
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
        services.AddFastEndpoints([..endpointTypes]);
        services.AddMcp(o => o.ToolVisibilityFilter = static (_, _, _) => true);

        var provider = services.BuildServiceProvider();

        foreach (var def in provider.GetRequiredService<EndpointData>().Found)
        {
            if (def.EndpointType == typeof(NoRequestEndpoint))
                def.McpTool("no_request");

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

            if (def.EndpointType == typeof(DuplicatePropertiesEndpoint))
                def.McpTool("duplicate_properties");
        }

        return provider;
    }

    [HttpPost("/mcp-schema-root/no-request")]
    sealed class NoRequestEndpoint : EndpointWithoutRequest<ObjectResponse>
    {
        public override Task HandleAsync(CancellationToken ct)
            => Send.OkAsync(new() { Value = "no-request" }, ct);
    }

    static McpServerTool BuildTool(IServiceProvider provider, string name)
        => provider.GetRequiredService<EndpointMcpToolSource>().BuildTools().Single(t => t.ProtocolTool.Name == name);

    static RequestContext<CallToolRequestParams> BuildRequestContext(IServiceProvider provider, McpServerTool tool, Dictionary<string, JsonElement> arguments)
        => McpToolVisibilityTestsBridge.BuildCallRequestContext(provider, tool.ProtocolTool.Name, BuildPrincipal(true), arguments, tool);

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

    [HttpPost("/mcp-schema-root/duplicate-properties")]
    sealed class DuplicatePropertiesEndpoint : EndpointWithoutRequest<DuplicatePropertiesResponse>
    {
        public override Task HandleAsync(CancellationToken ct)
            => Send.StringAsync("""{"id":1,"other":true,"id":2}""", 200, "application/json", ct);
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    sealed class DuplicatePropertiesResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public int Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("other")]
        public bool Other { get; set; }
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    sealed class ObjectRequest
    {
        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
        [System.ComponentModel.Description("Value to echo")]
        public string Value { get; set; } = "";
    }

    sealed class ObjectResponse
    {
        [System.ComponentModel.Description("Echoed value")]
        public string? Value { get; set; }
    }
}
