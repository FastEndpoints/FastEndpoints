using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
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
        protocolTool.InputSchema.GetProperty("additionalProperties").GetBoolean().ShouldBeFalse();
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
    public void Hidden_transport_inputs_are_not_advertised_as_client_arguments()
    {
        using var provider = BuildServices();

        var tool = BuildTool(provider, "hidden_transport_input_tool");
        var props = tool.ProtocolTool.InputSchema.GetProperty("properties");

        props.TryGetProperty("InternalHeader", out _).ShouldBeFalse();
        props.TryGetProperty("InternalCookie", out _).ShouldBeFalse();
        props.TryGetProperty("Value", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Hidden_transport_inputs_are_not_bound_from_client_arguments()
    {
        using var provider = BuildServices();

        var tool = BuildTool(provider, "hidden_transport_input_tool");
        var result = await tool.InvokeAsync(
            BuildRequestContext(
                provider,
                tool,
                authenticated: true,
                new()
                {
                    ["InternalHeader"] = JsonSerializer.SerializeToElement("spoofed-header"),
                    ["InternalCookie"] = JsonSerializer.SerializeToElement("spoofed-cookie"),
                    ["Value"] = JsonSerializer.SerializeToElement("ping")
                }),
            CancellationToken.None);

        result.StructuredContent!.Value.GetProperty("Value").GetString().ShouldBe("none:none:ping");
    }

    [Fact]
    public async Task ToHeader_response_properties_are_not_advertised_as_structured_content()
    {
        using var provider = BuildServices();
        SetUser(provider, true);

        var tool = BuildTool(provider, "to_header_output_tool");
        var outputProps = tool.ProtocolTool.OutputSchema!.Value.GetProperty("properties");

        outputProps.TryGetProperty("HeaderValue", out _).ShouldBeFalse();
        outputProps.TryGetProperty("Value", out _).ShouldBeTrue();

        var result = await tool.InvokeAsync(BuildRequestContext(provider, tool, authenticated: true), CancellationToken.None);
        var structured = result.StructuredContent!.Value;

        structured.TryGetProperty("HeaderValue", out _).ShouldBeFalse();
        structured.GetProperty("Value").GetString().ShouldBe("body:ping");
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
    public void Attribute_explicit_false_hints_are_preserved()
    {
        using var provider = BuildServices();

        var tool = BuildTool(provider, "attribute_false_tool");
        var protocolTool = tool.ProtocolTool;

        protocolTool.Annotations.ShouldNotBeNull();
        protocolTool.Annotations.DestructiveHint.ShouldBe(false);
        protocolTool.Annotations.OpenWorldHint.ShouldBe(false);
    }

    [Fact]
    public void Attribute_omitted_hints_remain_null()
    {
        using var provider = BuildServices();

        var tool = BuildTool(provider, "attribute_omitted_tool");
        var protocolTool = tool.ProtocolTool;

        protocolTool.Annotations.ShouldBeNull();
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

    [Theory]
    [InlineData("mismatched_type_output_tool")]
    [InlineData("missing_required_output_tool")]
    [InlineData("unknown_nested_output_tool")]
    public async Task Structured_content_is_omitted_when_response_violates_output_schema(string toolName)
    {
        using var provider = BuildServices();

        var tool = BuildTool(provider, toolName);
        var result = await tool.InvokeAsync(BuildRequestContext(provider, tool, authenticated: true), CancellationToken.None);

        result.IsError.ShouldNotBe(true);
        result.StructuredContent.HasValue.ShouldBeFalse();
        ((TextContentBlock)result.Content[0]).Text.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Endpoint_serializer_context_controls_tool_schemas_and_invocation_payloads()
    {
        using var provider = BuildServices();
        SetUser(provider, true);

        var snakeTool = BuildTool(provider, "snake_context_tool");
        var kebabTool = BuildTool(provider, "kebab_context_tool");

        var snakeInputProps = snakeTool.ProtocolTool.InputSchema.GetProperty("properties");
        var snakeOutputProps = snakeTool.ProtocolTool.OutputSchema!.Value.GetProperty("properties");
        snakeInputProps.TryGetProperty("request_value", out _).ShouldBeTrue();
        snakeOutputProps.TryGetProperty("response_value", out _).ShouldBeTrue();

        var kebabInputProps = kebabTool.ProtocolTool.InputSchema.GetProperty("properties");
        var kebabOutputProps = kebabTool.ProtocolTool.OutputSchema!.Value.GetProperty("properties");
        kebabInputProps.TryGetProperty("request-value", out _).ShouldBeTrue();
        kebabOutputProps.TryGetProperty("response-value", out _).ShouldBeTrue();

        var snakeResult = await snakeTool.InvokeAsync(
            BuildRequestContext(provider, snakeTool, authenticated: true, new() { ["request_value"] = JsonSerializer.SerializeToElement("snake") }),
            CancellationToken.None);
        var kebabResult = await kebabTool.InvokeAsync(
            BuildRequestContext(provider, kebabTool, authenticated: true, new() { ["request-value"] = JsonSerializer.SerializeToElement("kebab") }),
            CancellationToken.None);

        snakeResult.StructuredContent!.Value.GetProperty("response_value").GetString().ShouldBe("snake");
        kebabResult.StructuredContent!.Value.GetProperty("response-value").GetString().ShouldBe("kebab");
    }

    [Fact]
    public void Validator_rules_apply_to_serializer_context_property_names()
    {
        using var provider = BuildServices();

        var tool = BuildTool(provider, "validated_context_tool");
        var inputSchema = tool.ProtocolTool.InputSchema;
        var required = inputSchema.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        var valueSchema = inputSchema.GetProperty("properties").GetProperty("request_value");

        required.ShouldContain("request_value");
        valueSchema.GetProperty("minLength").GetInt32().ShouldBe(3);
    }

    [Fact]
    public void Validator_schema_enrichment_supports_scoped_validator_dependencies()
    {
        using var provider = BuildServices(validateScopes: true);

        var tool = BuildTool(provider, "scoped_validator_tool");
        var required = tool.ProtocolTool.InputSchema.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();

        required.ShouldContain("Value");
    }

    [Fact]
    public async Task Principal_bound_properties_are_not_advertised_as_client_arguments()
    {
        using var provider = BuildServices();

        var principal = new ClaimsPrincipal(new ClaimsIdentity([new("sub", "caller"), new("tenant_id", "actual")], "test"));
        var tool = BuildTool(provider, "principal_bound_tool");
        var inputSchema = tool.ProtocolTool.InputSchema;
        var props = inputSchema.GetProperty("properties");

        props.TryGetProperty("TenantId", out _).ShouldBeFalse();
        props.TryGetProperty("CanEdit", out _).ShouldBeFalse();
        props.TryGetProperty("OptionalTenantId", out _).ShouldBeTrue();
        props.TryGetProperty("Value", out _).ShouldBeTrue();

        if (inputSchema.TryGetProperty("required", out var required))
            required.EnumerateArray().Select(x => x.GetString()).ShouldNotContain("TenantId");

        var result = await tool.InvokeAsync(
            McpToolVisibilityTests_Bridge.BuildCallRequestContext(
                provider,
                tool.ProtocolTool.Name,
                principal,
                new()
                {
                    ["TenantId"] = JsonSerializer.SerializeToElement("spoofed"),
                    ["OptionalTenantId"] = JsonSerializer.SerializeToElement("fallback"),
                    ["CanEdit"] = JsonSerializer.SerializeToElement(true),
                    ["Value"] = JsonSerializer.SerializeToElement("ping")
                },
                tool),
            CancellationToken.None);

        result.StructuredContent!.Value.GetProperty("Value").GetString().ShouldBe("actual:fallback:False:ping");
    }

    [Fact]
    public async Task Endpoint_exceptions_are_returned_as_generic_tool_errors()
    {
        using var provider = BuildServices();

        var tool = BuildTool(provider, "faulted_tool");
        var result = await tool.InvokeAsync(BuildRequestContext(provider, tool, authenticated: true), CancellationToken.None);

        result.IsError.ShouldBe(true);
        ((TextContentBlock)result.Content[0]).Text.ShouldContain("Endpoint invocation failed.");
        ((TextContentBlock)result.Content[0]).Text.ShouldNotContain("faulted endpoint");
    }

    static ServiceProvider BuildServices(Action<McpOptions>? configure = null, bool validateScopes = false)
    {
        Factory.RegisterTestServices(
            s =>
            {
                s.AddSingleton(typeof(IRequestBinder<>), typeof(RequestBinder<>));
                s.AddSingleton<IValidator<SerializerContextToolRequest>, SerializerContextToolRequestValidator>();
                s.AddScoped<ScopedValidatorDependency>();
                s.AddScoped<IValidator<ScopedValidatorToolRequest>, ScopedValidatorToolRequestValidator>();
            });

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddScoped<ScopedValidatorDependency>();
        services.AddFastEndpoints(
            o =>
            {
                o.SourceGeneratorDiscoveredTypes.Add(typeof(DescriptorToolEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(AttributeToolEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(AttributeFalseToolEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(AttributeOmittedHintsToolEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(SnakeCaseContextToolEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(KebabCaseContextToolEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(ValidatedContextToolEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(SerializerContextToolRequestValidator));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(ScopedValidatorToolEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(ScopedValidatorToolRequestValidator));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(PrincipalBoundToolEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(FaultedToolEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(HiddenTransportInputToolEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(ToHeaderOutputToolEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(MismatchedTypeOutputToolEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(MissingRequiredOutputToolEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(UnknownNestedOutputToolEndpoint));
            });
        services.AddMcp(
            o =>
            {
                o.ToolVisibilityFilter = static (_, _, _) => true;
                configure?.Invoke(o);
            });

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = validateScopes });

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

            if (def.EndpointType == typeof(SnakeCaseContextToolEndpoint))
            {
                def.SerializerContext = SnakeCaseMcpJsonContext.Default;
                def.McpTool("snake_context_tool");
            }

            if (def.EndpointType == typeof(KebabCaseContextToolEndpoint))
            {
                def.SerializerContext = KebabCaseMcpJsonContext.Default;
                def.McpTool("kebab_context_tool");
            }

            if (def.EndpointType == typeof(ValidatedContextToolEndpoint))
            {
                def.SerializerContext = SnakeCaseMcpJsonContext.Default;
                def.McpTool("validated_context_tool");
            }

            if (def.EndpointType == typeof(ScopedValidatorToolEndpoint))
                def.McpTool("scoped_validator_tool");

            if (def.EndpointType == typeof(PrincipalBoundToolEndpoint))
                def.McpTool("principal_bound_tool");

            if (def.EndpointType == typeof(FaultedToolEndpoint))
                def.McpTool("faulted_tool");

            if (def.EndpointType == typeof(HiddenTransportInputToolEndpoint))
                def.McpTool("hidden_transport_input_tool");

            if (def.EndpointType == typeof(ToHeaderOutputToolEndpoint))
                def.McpTool("to_header_output_tool");

            if (def.EndpointType == typeof(MismatchedTypeOutputToolEndpoint))
                def.McpTool("mismatched_type_output_tool");

            if (def.EndpointType == typeof(MissingRequiredOutputToolEndpoint))
                def.McpTool("missing_required_output_tool");

            if (def.EndpointType == typeof(UnknownNestedOutputToolEndpoint))
                def.McpTool("unknown_nested_output_tool");
        }

        return provider;
    }

    static McpServerTool BuildTool(IServiceProvider provider, string name)
        => provider.GetRequiredService<EndpointMcpToolSource>().BuildTools().Single(t => t.ProtocolTool.Name == name);

    static RequestContext<CallToolRequestParams> BuildRequestContext(IServiceProvider provider,
                                                                     McpServerTool tool,
                                                                     bool authenticated,
                                                                     Dictionary<string, JsonElement>? arguments = null)
    {
        var user = authenticated
                       ? new ClaimsPrincipal(new ClaimsIdentity([new("sub", "caller")], "test"))
                       : new ClaimsPrincipal(new ClaimsIdentity());

        return McpToolVisibilityTests_Bridge.BuildCallRequestContext(provider, tool.ProtocolTool.Name, user, arguments, tool);
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

    [McpTool("attribute_false_tool", Description = "Explicit false hints.", Destructive = false, OpenWorld = false)]
    [HttpPost("/attribute-false-tool")]
    sealed class AttributeFalseToolEndpoint : Endpoint<ToolRequest, ToolResponse>
    {
        public override async Task HandleAsync(ToolRequest req, CancellationToken ct)
            => await Send.OkAsync(new() { Value = "attribute-false:" + req.Value }, ct);
    }

    [McpTool("attribute_omitted_tool", Description = "Hints omitted.")]
    [HttpPost("/attribute-omitted-tool")]
    sealed class AttributeOmittedHintsToolEndpoint : Endpoint<ToolRequest, ToolResponse>
    {
        public override async Task HandleAsync(ToolRequest req, CancellationToken ct)
            => await Send.OkAsync(new() { Value = "attribute-omitted:" + req.Value }, ct);
    }

    sealed class ToolRequest
    {
        public string Value { get; set; } = "";
    }

    sealed class ToolResponse
    {
        public string? Value { get; set; }
    }

    [HttpPost("/descriptor-tool/snake-context")]
    sealed class SnakeCaseContextToolEndpoint : Endpoint<SerializerContextToolRequest, SerializerContextToolResponse>
    {
        public override Task HandleAsync(SerializerContextToolRequest req, CancellationToken ct)
            => Send.OkAsync(new() { ResponseValue = req.RequestValue }, ct);
    }

    [HttpPost("/descriptor-tool/kebab-context")]
    sealed class KebabCaseContextToolEndpoint : Endpoint<SerializerContextToolRequest, SerializerContextToolResponse>
    {
        public override Task HandleAsync(SerializerContextToolRequest req, CancellationToken ct)
            => Send.OkAsync(new() { ResponseValue = req.RequestValue }, ct);
    }

    [HttpPost("/descriptor-tool/validated-context")]
    sealed class ValidatedContextToolEndpoint : Endpoint<SerializerContextToolRequest, SerializerContextToolResponse>
    {
        public override Task HandleAsync(SerializerContextToolRequest req, CancellationToken ct)
            => Send.OkAsync(new() { ResponseValue = req.RequestValue }, ct);
    }

    [HttpPost("/descriptor-tool/scoped-validator")]
    sealed class ScopedValidatorToolEndpoint : Endpoint<ScopedValidatorToolRequest, ToolResponse>
    {
        public override Task HandleAsync(ScopedValidatorToolRequest req, CancellationToken ct)
            => Send.OkAsync(new() { Value = req.Value }, ct);
    }

    sealed class ScopedValidatorToolRequest
    {
        public string Value { get; set; } = "";
    }

    sealed class ScopedValidatorToolRequestValidator : Validator<ScopedValidatorToolRequest>
    {
        public ScopedValidatorToolRequestValidator(ScopedValidatorDependency dependency)
        {
            _ = dependency;

            RuleFor(x => x.Value).NotEmpty();
        }
    }

    sealed class ScopedValidatorDependency;

    [HttpPost("/descriptor-tool/principal-bound")]
    sealed class PrincipalBoundToolEndpoint : Endpoint<PrincipalBoundToolRequest, ToolResponse>
    {
        public override Task HandleAsync(PrincipalBoundToolRequest req, CancellationToken ct)
            => Send.OkAsync(new() { Value = $"{req.TenantId}:{req.OptionalTenantId}:{req.CanEdit}:{req.Value}" }, ct);
    }

    sealed class PrincipalBoundToolRequest
    {
        [FromClaim("tenant_id")]
        public string TenantId { get; set; } = "";

        [FromClaim("optional_tenant", isRequired: false)]
        public string? OptionalTenantId { get; set; }

        [HasPermission("Edit_Item", isRequired: false)]
        public bool CanEdit { get; set; }

        public string Value { get; set; } = "";
    }

    [HttpPost("/descriptor-tool/faulted")]
    sealed class FaultedToolEndpoint : Endpoint<ToolRequest, ToolResponse>
    {
        public override Task HandleAsync(ToolRequest req, CancellationToken ct)
            => throw new InvalidOperationException("faulted endpoint");
    }

    [HttpPost("/descriptor-tool/hidden-transport-input")]
    sealed class HiddenTransportInputToolEndpoint : Endpoint<HiddenTransportInputToolRequest, ToolResponse>
    {
        public override Task HandleAsync(HiddenTransportInputToolRequest req, CancellationToken ct)
            => Send.OkAsync(new() { Value = $"{TransportValue(req.InternalHeader)}:{TransportValue(req.InternalCookie)}:{req.Value}" }, ct);

        static string TransportValue(string? value)
            => string.IsNullOrEmpty(value) ? "none" : value;
    }

    sealed class HiddenTransportInputToolRequest
    {
        [FromHeader("x-internal", isRequired: false, removeFromSchema: true)]
        public string? InternalHeader { get; set; }

        [FromCookie("internal", isRequired: false, removeFromSchema: true)]
        public string? InternalCookie { get; set; }

        public string Value { get; set; } = "";
    }

    [HttpPost("/descriptor-tool/to-header-output")]
    sealed class ToHeaderOutputToolEndpoint : Endpoint<ToolRequest, ToHeaderToolResponse>
    {
        public override Task HandleAsync(ToolRequest req, CancellationToken ct)
            => Send.OkAsync(new() { HeaderValue = "header:" + req.Value, Value = "body:" + req.Value }, ct);
    }

    sealed class ToHeaderToolResponse
    {
        [ToHeader("x-tool-value")]
        public string? HeaderValue { get; set; }

        public string? Value { get; set; }
    }

    [HttpPost("/descriptor-tool/mismatched-type-output")]
    sealed class MismatchedTypeOutputToolEndpoint : Endpoint<ToolRequest, ToolResponse>
    {
        public override Task HandleAsync(ToolRequest req, CancellationToken ct)
            => Send.StringAsync("""{"Value":123}""", 200, "application/json", ct);
    }

    [HttpPost("/descriptor-tool/missing-required-output")]
    sealed class MissingRequiredOutputToolEndpoint : Endpoint<ToolRequest, RequiredOutputResponse>
    {
        public override Task HandleAsync(ToolRequest req, CancellationToken ct)
            => Send.StringAsync("{}", 200, "application/json", ct);
    }

    sealed class RequiredOutputResponse
    {
        public required string Value { get; set; }
    }

    [HttpPost("/descriptor-tool/unknown-nested-output")]
    sealed class UnknownNestedOutputToolEndpoint : Endpoint<ToolRequest, NestedOutputResponse>
    {
        public override Task HandleAsync(ToolRequest req, CancellationToken ct)
            => Send.StringAsync("""{"Nested":{"Value":"ok","Secret":"leak"}}""", 200, "application/json", ct);
    }

    sealed class NestedOutputResponse
    {
        public NestedOutputValue Nested { get; set; } = new();
    }

    sealed class NestedOutputValue
    {
        public string? Value { get; set; }
    }
}

sealed class SerializerContextToolRequest
{
    public string RequestValue { get; set; } = "";
}

sealed class SerializerContextToolResponse
{
    public string? ResponseValue { get; set; }
}

sealed class SerializerContextToolRequestValidator : Validator<SerializerContextToolRequest>
{
    public SerializerContextToolRequestValidator()
    {
        RuleFor(x => x.RequestValue).NotEmpty().MinimumLength(3);
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(SerializerContextToolRequest))]
[JsonSerializable(typeof(SerializerContextToolResponse))]
partial class SnakeCaseMcpJsonContext : JsonSerializerContext;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.KebabCaseLower)]
[JsonSerializable(typeof(SerializerContextToolRequest))]
[JsonSerializable(typeof(SerializerContextToolResponse))]
partial class KebabCaseMcpJsonContext : JsonSerializerContext;

static class McpToolVisibilityTests_Bridge
{
    public static RequestContext<CallToolRequestParams> BuildRequestContext(IServiceProvider provider, McpServerTool tool, ClaimsPrincipal user)
        => BuildCallRequestContext(provider, tool.ProtocolTool.Name, user, new Dictionary<string, JsonElement> { ["Value"] = JsonSerializer.SerializeToElement("ping") }, tool);

    public static RequestContext<CallToolRequestParams> BuildCallRequestContext(IServiceProvider provider,
                                                                                string toolName,
                                                                                ClaimsPrincipal user,
                                                                                Dictionary<string, JsonElement>? arguments = null,
                                                                                McpServerTool? matchedTool = null)
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
                Name = toolName,
                Arguments = arguments ?? new Dictionary<string, JsonElement> { ["Value"] = JsonSerializer.SerializeToElement("ping") }
            })
        {
            Services = provider,
            User = user,
            MatchedPrimitive = matchedTool
        };
    }

    public static RequestContext<ListToolsRequestParams> BuildListRequestContext(IServiceProvider provider, ClaimsPrincipal user)
    {
        var request = new JsonRpcRequest
        {
            Id = new RequestId(1),
            Method = RequestMethods.ToolsList
        };
        var server = McpServer.Create(new TestTransport(), new McpServerOptions(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, provider);

        return new(
            server,
            request,
            new ListToolsRequestParams())
        {
            Services = provider,
            User = user
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
