using System.Security.Claims;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints.Agents;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FastEndpoints.Mcp;

/// <summary>
/// builds <see cref="McpServerTool" /> instances for every FastEndpoints endpoint that opted-in via
/// <c>McpTool(...)</c> or <c>[McpTool]</c>. tools are registered into the MCP server's primary tool
/// collection at <c>MapMcp</c> time; per-session filtering (auth visibility) is applied
/// inside each tool's invocation handler because tool visibility depends on <c>HttpContext.User</c>,
/// which is only available at call time.
/// </summary>
sealed class EndpointMcpToolSource(IServiceProvider services, McpOptions options, ILogger<EndpointMcpToolSource> logger)
{
    static readonly JsonElement _emptyArguments = JsonDocument.Parse("{}").RootElement.Clone();

    readonly Lazy<AgentEndpointCatalog<ToolRegistration>> _toolCatalog = new(() => BuildToolCatalog(services, options, logger));

    public IReadOnlyList<McpServerTool> BuildTools()
    {
        _toolCatalog.Value.EnsureUnique(_toolCatalog.Value.Entries, "registered MCP tools");

        return _toolCatalog.Value.Entries.Select(x => x.Tool).ToArray();
    }

    public IReadOnlyList<Tool> BuildVisibleProtocolTools(ClaimsPrincipal? user = null, HttpContext? httpContext = null)
    {
        var visibleTools = GetVisibleTools(user, httpContext);

        _toolCatalog.Value.EnsureUnique(visibleTools, "MCP tools visible to the current caller");

        return visibleTools.Select(x => x.Tool.ProtocolTool).ToArray();
    }

    public McpServerTool? ResolveVisibleTool(string name, ClaimsPrincipal? user = null, HttpContext? httpContext = null)
    {
        var (principal, context) = ResolveCallerContext(user, httpContext);
        var tool = _toolCatalog.Value.ResolveVisible(name, principal, context, options.ToolVisibilityFilter, "MCP tools visible to the current caller", "MCP tool name");

        return tool?.Tool;
    }

    (ClaimsPrincipal Principal, HttpContext HttpContext) ResolveCallerContext(ClaimsPrincipal? user, HttpContext? httpContext)
        => FastEndpoints.Agents.CallerContextResolver.Resolve(services, user, httpContext);

    IReadOnlyList<ToolRegistration> GetVisibleTools(ClaimsPrincipal? user, HttpContext? httpContext)
    {
        var (principal, context) = ResolveCallerContext(user, httpContext);

        return _toolCatalog.Value.GetVisible(principal, context, options.ToolVisibilityFilter);
    }

    static AgentEndpointCatalog<ToolRegistration> BuildToolCatalog(IServiceProvider services, McpOptions options, ILogger<EndpointMcpToolSource> logger)
    {
        var protocolSerializerOptions = AgentJsonSerializerOptions.EnsureTypeInfoResolver(Config.SerOpts.Options);
        var invoker = services.GetRequiredService<EndpointInvoker>();

        return AgentEndpointCatalog<ToolRegistration>.FromEndpoints(
            services,
            def =>
            {
                var info = def.ResolveToolInfo();

                if (info is null)
                    return null;

                if (options.ToolFilter is not null && !options.ToolFilter(def))
                    return null;

                var tool = BuildTool(def, info, invoker, ResolveSchemaSerializerOptions(def, protocolSerializerOptions), protocolSerializerOptions, services, options, logger);

                return new(def, tool.ProtocolTool.Name, tool);
            },
            x => x.Name,
            x => x.Definition,
            "MCP tool names");
    }

    static McpServerTool BuildTool(EndpointDefinition def,
                                   McpToolInfo info,
                                   EndpointInvoker invoker,
                                   JsonSerializerOptions schemaSerializerOptions,
                                   JsonSerializerOptions protocolSerializerOptions,
                                   IServiceProvider services,
                                   McpOptions options,
                                   ILogger<EndpointMcpToolSource> logger)
    {
        var summaryTitle = def.EndpointSummary?.Summary;
        var name = McpToolNameResolver.ResolvePublishedName(def, info);
        var description = info.Description ?? def.EndpointSummary?.Description;

        if (info.Name is null && string.IsNullOrWhiteSpace(summaryTitle))
        {
            logger.LogWarning(
                "MCP tool for {EndpointType} has no explicit name and no OpenAPI Summary set. " +
                "Falling back to type name \"{FallbackName}\". " +
                "Set Summary(s => s.Summary = ...) or pass the name explicitly.",
                def.EndpointType.Name,
                name);
        }

        if (info.Description is null && string.IsNullOrWhiteSpace(def.EndpointSummary?.Description))
        {
            logger.LogWarning(
                "MCP tool \"{Name}\" ({EndpointType}) has no description. " +
                "Call Summary(s => s.Description = ...) or pass an explicit description: this.McpTool(description: \"...\").",
                name,
                def.EndpointType.Name);
        }

        var inputSchema = BuildInputSchema(def, schemaSerializerOptions, name);
        RemoveNonClientInputProperties(inputSchema, def, schemaSerializerOptions);
        EnrichInputSchemaWithValidation(inputSchema, def, schemaSerializerOptions, services);
        DisallowAdditionalInputProperties(inputSchema);

        var outputSchema = TryBuildOutputSchema(def, schemaSerializerOptions, options);

        var tool = McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> ctx, CancellationToken ct) =>
            {
                var scopedServices = ctx.Services ?? services;
                var (principal, httpContext) = CallerContextResolver.Resolve(scopedServices, ctx.User);

                if (!options.ToolVisibilityFilter(def, principal, httpContext))
                    throw new McpException($"Tool '{name}' is not available for the current caller.");

                var args = ctx.Params.Arguments is { } argMap
                               ? JsonSerializer.SerializeToElement(argMap, protocolSerializerOptions)
                               : _emptyArguments;

                var result = await invoker.InvokeAsync(def, args, principal, ct);

                return result.Status switch
                {
                    InvocationStatus.Success => BuildSuccessResult(result, outputSchema),
                    InvocationStatus.HttpError => BuildHttpErrorResult(result),
                    InvocationStatus.ValidationFailed => BuildValidationErrorResult(result),
                    InvocationStatus.Faulted => BuildFaultedResult(result, logger, name, def),
                    _ => throw new McpException("Unknown invocation status.")
                };
            },
            new()
            {
                Name = name,
                Description = description,
                Title = info.Title,
                ReadOnly = info.Hints.ReadOnly,
                Idempotent = info.Hints.Idempotent,
                Destructive = info.Hints.Destructive,
                OpenWorld = info.Hints.OpenWorld,
                UseStructuredContent = outputSchema is not null,
                OutputSchema = outputSchema is null ? null : JsonSerializer.SerializeToElement(outputSchema, protocolSerializerOptions),
                SerializerOptions = protocolSerializerOptions
            });

        tool.ProtocolTool.InputSchema = JsonSerializer.SerializeToElement(inputSchema, protocolSerializerOptions);

        return tool;
    }

    static CallToolResult BuildSuccessResult(InvocationResult r, JsonNode? outputSchema)
    {
        var text = InvocationResultHelpers.ReadBodyText(r);
        JsonElement? structuredContent = null;

        if (outputSchema is not null && TryExtractStructuredContent(text, out var content) && TryApplyOutputSchema(content, outputSchema, out var schemaContent))
            structuredContent = schemaContent;

        return new()
        {
            Content = [new TextContentBlock { Text = text }],
            StructuredContent = structuredContent,
            IsError = false
        };
    }

    static CallToolResult BuildValidationErrorResult(InvocationResult r)
    {
        var validationErrors = r.ValidationFailures.Select(f => new { property = f.PropertyName, error = f.ErrorMessage, code = f.ErrorCode });
        var json = JsonSerializer.Serialize(new { validationErrors });

        return new()
        {
            Content = [new TextContentBlock { Text = json }],
            IsError = true
        };
    }

    static CallToolResult BuildHttpErrorResult(InvocationResult r)
    {
        var text = InvocationResultHelpers.ReadBodyText(r);

        return new()
        {
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(new { statusCode = r.HttpStatusCode, contentType = r.ContentType, body = text }) }],
            IsError = true
        };
    }

    static CallToolResult BuildFaultedResult(InvocationResult r, ILogger logger, string toolName, EndpointDefinition def)
    {
        if (r.Exception is { } ex)
            logger.LogError(ex, "MCP tool {ToolName} failed while invoking endpoint {EndpointType}.", toolName, def.EndpointType.FullName ?? def.EndpointType.Name);

        return new()
        {
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(new { error = "Endpoint invocation failed." }) }],
            IsError = true
        };
    }

    static JsonNode BuildInputSchema(EndpointDefinition def, JsonSerializerOptions serializerOptions, string toolName)
    {
        if (def.ReqDtoType == Types.EmptyRequest)
            return new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject(),
                ["additionalProperties"] = false
            };

        var inputSchema = JsonSchemaBuilder.Build(def.ReqDtoType, serializerOptions);
        NormalizeRootObjectSchema(inputSchema);
        EnsureObjectRootSchema(inputSchema, toolName, def.ReqDtoType, "input", "arguments");

        return inputSchema;
    }

    static JsonNode? TryBuildOutputSchema(EndpointDefinition def, JsonSerializerOptions serializerOptions, McpOptions options)
    {
        if (!options.IncludeOutputSchemas || def.ResDtoType == typeof(object) || def.ResDtoType == typeof(void))
            return null;

        var outputSchema = JsonSchemaBuilder.Build(def.ResDtoType, serializerOptions);
        NormalizeRootObjectSchema(outputSchema);

        if (HasObjectRootSchema(outputSchema))
            RemoveToHeaderOutputProperties(outputSchema, def, serializerOptions);

        return HasObjectRootSchema(outputSchema) ? outputSchema : null;
    }

    static void NormalizeRootObjectSchema(JsonNode schema)
    {
        if (schema is not JsonObject obj || obj["type"] is not JsonArray types)
            return;

        foreach (var type in types)
        {
            if (type?.GetValue<string>() == "object")
            {
                obj["type"] = "object";

                return;
            }
        }
    }

    static void EnsureObjectRootSchema(JsonNode schema, string toolName, Type dtoType, string schemaKind, string shapeDescription)
    {
        if (HasObjectRootSchema(schema))
            return;

        throw new InvalidOperationException(
            $"MCP tool '{toolName}' cannot use {schemaKind} schema generated from '{dtoType.FullName ?? dtoType.Name}' with root type '{GetRootSchemaType(schema)}' because MCP tools require an object root schema. Use an object-shaped DTO for tool {shapeDescription}.");
    }

    static bool HasObjectRootSchema(JsonNode schema)
    {
        if (schema is not JsonObject obj || obj["type"] is not JsonNode typeNode)
            return false;

        return typeNode switch
        {
            JsonValue value => value.TryGetValue<string>(out var type) && type == "object",
            JsonArray types => types.Any(type => type is JsonValue value && value.TryGetValue<string>(out var entry) && entry == "object"),
            _ => false
        };
    }

    static string GetRootSchemaType(JsonNode schema)
    {
        if (schema is not JsonObject obj || obj["type"] is not JsonNode typeNode)
            return "<unspecified>";

        return typeNode switch
        {
            JsonValue value when value.TryGetValue<string>(out var type) => type,
            JsonArray types => string.Join("|", types.Select(type => type?.ToJsonString() ?? "null")),
            _ => typeNode.ToJsonString()
        };
    }

    static JsonSerializerOptions ResolveSchemaSerializerOptions(EndpointDefinition def, JsonSerializerOptions fallback)
        => def.SerializerContext?.Options is { } options
               ? AgentJsonSerializerOptions.EnsureTypeInfoResolver(options)
               : fallback;

    static bool TryExtractStructuredContent(string text, out JsonElement structuredContent)
    {
        if (InvocationResultHelpers.TryParseJson(text, out var json) && json.ValueKind == JsonValueKind.Object)
        {
            structuredContent = json;

            return true;
        }

        structuredContent = default;

        return false;
    }

    static bool TryApplyOutputSchema(JsonElement content, JsonNode outputSchema, out JsonElement structuredContent)
    {
        if (outputSchema is not JsonObject root || root["properties"] is not JsonObject props)
        {
            structuredContent = content;

            return true;
        }

        if (!ValidateJsonValue(content, root, requireKnownObjectProperties: false))
        {
            structuredContent = default;

            return false;
        }

        var allowedProps = props.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        var filtered = new JsonObject();

        foreach (var prop in content.EnumerateObject())
        {
            if (allowedProps.Contains(prop.Name))
                filtered[prop.Name] = JsonNode.Parse(prop.Value.GetRawText());
        }

        structuredContent = JsonSerializer.SerializeToElement(filtered);

        return true;
    }

    static bool ValidateJsonValue(JsonElement value, JsonObject schema, bool requireKnownObjectProperties)
    {
        if (!MatchesSchemaType(value, schema["type"]))
            return false;

        if (value.ValueKind == JsonValueKind.Object)
            return ValidateJsonObject(value, schema, requireKnownObjectProperties);

        if (value.ValueKind == JsonValueKind.Array && schema["items"] is JsonObject itemSchema)
        {
            foreach (var item in value.EnumerateArray())
            {
                if (!ValidateJsonValue(item, itemSchema, requireKnownObjectProperties: true))
                    return false;
            }
        }

        return true;
    }

    static bool ValidateJsonObject(JsonElement value, JsonObject schema, bool requireKnownObjectProperties)
    {
        var props = schema["properties"] as JsonObject;

        if (schema["required"] is JsonArray required)
        {
            foreach (var requiredProp in required)
            {
                if (requiredProp?.GetValue<string>() is { } name && !value.TryGetProperty(name, out _))
                    return false;
            }
        }

        if (props is null)
            return true;

        foreach (var prop in value.EnumerateObject())
        {
            if (!props.TryGetPropertyValue(prop.Name, out var propSchema))
            {
                if (requireKnownObjectProperties)
                    return false;

                continue;
            }

            if (propSchema is JsonObject propSchemaObject && !ValidateJsonValue(prop.Value, propSchemaObject, requireKnownObjectProperties: true))
                return false;
        }

        return true;
    }

    static bool MatchesSchemaType(JsonElement value, JsonNode? typeNode)
    {
        if (typeNode is null)
            return true;

        if (typeNode is JsonValue typeValue && typeValue.TryGetValue<string>(out var type))
            return MatchesSchemaType(value, type);

        if (typeNode is JsonArray types)
            return types.Any(t => t is JsonValue item && item.TryGetValue<string>(out var type) && MatchesSchemaType(value, type));

        return true;
    }

    static bool MatchesSchemaType(JsonElement value, string type)
        => type switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "number" => value.ValueKind == JsonValueKind.Number,
            "null" => value.ValueKind == JsonValueKind.Null,
            _ => true
        };

    static void EnrichInputSchemaWithValidation(JsonNode inputSchema, EndpointDefinition def, JsonSerializerOptions serializerOptions, IServiceProvider services)
    {
        if (def.ValidatorType is null)
            return;

        using var scope = services.CreateScope();

        if (TryResolveValidator(scope.ServiceProvider, def.ValidatorType) is { } validator)
            FluentValidationSchemaEnricher.Enrich(inputSchema, validator, def.ReqDtoType, serializerOptions);
    }

    static void DisallowAdditionalInputProperties(JsonNode inputSchema)
    {
        if (inputSchema is JsonObject root && HasObjectRootSchema(root))
            root["additionalProperties"] = false;
    }

    static IValidator? TryResolveValidator(IServiceProvider services, Type validatorType)
    {
        return services.GetService(validatorType) as IValidator ?? (IValidator?)ActivatorUtilities.CreateInstance(services, validatorType);
    }

    static void RemoveNonClientInputProperties(JsonNode inputSchema, EndpointDefinition def, JsonSerializerOptions serializerOptions)
        => RemoveSchemaProperties(inputSchema, def.ReqDtoType, serializerOptions, AgentInputPropertyRules.ShouldIgnoreClientInput);

    static void RemoveToHeaderOutputProperties(JsonNode outputSchema, EndpointDefinition def, JsonSerializerOptions serializerOptions)
        => RemoveSchemaProperties(outputSchema, def.ResDtoType, serializerOptions, prop => prop.IsDefined(Types.ToHeaderAttribute, true));

    static void RemoveSchemaProperties(JsonNode schema, Type dtoType, JsonSerializerOptions serializerOptions, Func<PropertyInfo, bool> shouldRemove)
    {
        if (schema is not JsonObject root || root["properties"] is not JsonObject props)
            return;

        var required = root["required"] as JsonArray;

        foreach (var prop in dtoType.BindableProps())
        {
            if (!shouldRemove(prop))
                continue;

            foreach (var name in AgentJsonPropertyNames.GetSchemaNameCandidates(prop, dtoType, serializerOptions))
            {
                props.Remove(name);
                RemoveRequiredEntry(required, name);
            }
        }

        if (required is { Count: 0 })
            root.Remove("required");
    }

    static void RemoveRequiredEntry(JsonArray? required, string name)
    {
        if (required is null)
            return;

        for (var i = required.Count - 1; i >= 0; i--)
        {
            if (required[i]?.GetValue<string>() == name)
                required.RemoveAt(i);
        }
    }

    sealed record ToolRegistration(EndpointDefinition Definition, string Name, McpServerTool Tool);
}
