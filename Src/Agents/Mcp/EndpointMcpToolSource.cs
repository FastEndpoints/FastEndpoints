using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
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

    readonly Lazy<ToolCatalog> _toolCatalog = new(() => BuildToolCatalog(services, options, logger));

    public IReadOnlyList<McpServerTool> BuildTools()
    {
        EnsureUniqueNames(_toolCatalog.Value.Tools, "registered MCP tools");

        return _toolCatalog.Value.Tools.Select(x => x.Tool).ToArray();
    }

    public IReadOnlyList<Tool> BuildVisibleProtocolTools(ClaimsPrincipal? user = null, HttpContext? httpContext = null)
    {
        var visibleTools = GetVisibleTools(user, httpContext);

        EnsureUniqueNames(visibleTools, "MCP tools visible to the current caller");

        return visibleTools.Select(x => x.Tool.ProtocolTool).ToArray();
    }

    public McpServerTool? ResolveVisibleTool(string name, ClaimsPrincipal? user = null, HttpContext? httpContext = null)
    {
        if (!_toolCatalog.Value.ToolsByName.TryGetValue(name, out var candidateTools))
            return null;

        var (principal, context) = ResolveCallerContext(user, httpContext);
        var matches = candidateTools.Where(x => options.ToolVisibilityFilter(x.Definition, principal, context)).ToArray();

        return matches.Length switch
        {
            0 => null,
            1 => matches[0].Tool,
            _ => throw CreateDuplicateNameException(name, matches, "MCP tools visible to the current caller")
        };
    }

    (ClaimsPrincipal Principal, HttpContext HttpContext) ResolveCallerContext(ClaimsPrincipal? user, HttpContext? httpContext)
        => CallerContextResolver.Resolve(services, user, httpContext);

    IReadOnlyList<ToolRegistration> GetVisibleTools(ClaimsPrincipal? user, HttpContext? httpContext)
    {
        var (principal, context) = ResolveCallerContext(user, httpContext);

        return _toolCatalog.Value.Tools.Where(x => options.ToolVisibilityFilter(x.Definition, principal, context)).ToArray();
    }

    static ToolCatalog BuildToolCatalog(IServiceProvider services, McpOptions options, ILogger<EndpointMcpToolSource> logger)
    {
        var endpointData = services.GetRequiredService<EndpointData>();
        var protocolSerializerOptions = EnsureTypeInfoResolver(Config.SerOpts.Options);
        var invoker = services.GetRequiredService<EndpointInvoker>();

        var tools = new List<ToolRegistration>();

        foreach (var def in endpointData.Found)
        {
            var info = def.ResolveToolInfo();

            if (info is null)
                continue;

            if (options.ToolFilter is not null && !options.ToolFilter(def))
                continue;

            var tool = BuildTool(def, info, invoker, ResolveSchemaSerializerOptions(def, protocolSerializerOptions), protocolSerializerOptions, services, options, logger);
            tools.Add(new(def, tool.ProtocolTool.Name, tool));
        }

        return new(tools, IndexToolsByName(tools));
    }

    static IReadOnlyDictionary<string, ToolRegistration[]> IndexToolsByName(IReadOnlyList<ToolRegistration> tools)
        => tools.GroupBy(x => x.Name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

    static void EnsureUniqueNames(IReadOnlyCollection<ToolRegistration> tools, string scope)
    {
        var collisions = tools.GroupBy(x => x.Name, StringComparer.Ordinal)
                              .Where(g => g.Count() > 1)
                              .ToArray();

        if (collisions.Length == 0)
            return;

        throw new InvalidOperationException(
            "Duplicate MCP tool names detected among " +
            scope +
            ": " +
            string.Join(
                "; ",
                collisions.Select(
                    g => $"'{g.Key}' => {FormatEndpointTypeNames(g)}")) +
            ". MCP tool names must be unique.");
    }

    static InvalidOperationException CreateDuplicateNameException(string name, IReadOnlyCollection<ToolRegistration> matches, string scope)
        => new(
            $"Duplicate MCP tool name '{name}' detected among {scope}: " +
            FormatEndpointTypeNames(matches) +
            ". MCP tool names must be unique.");

    static string FormatEndpointTypeNames(IEnumerable<ToolRegistration> tools)
        => string.Join(", ", tools.Select(x => x.Definition.EndpointType.FullName ?? x.Definition.EndpointType.Name).Distinct(StringComparer.Ordinal));

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
                    InvocationStatus.Faulted => throw new McpException(result.Exception?.Message ?? "Endpoint invocation failed.", result.Exception),
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

        if (outputSchema is not null && TryExtractStructuredContent(text, out var content))
            structuredContent = content;

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

    static JsonNode BuildInputSchema(EndpointDefinition def, JsonSerializerOptions serializerOptions, string toolName)
    {
        if (def.ReqDtoType == Types.EmptyRequest)
            return new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject()
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

    static JsonSerializerOptions EnsureTypeInfoResolver(JsonSerializerOptions options)
        => options.TypeInfoResolver is not null
               ? options
               : new JsonSerializerOptions(options) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    static JsonSerializerOptions ResolveSchemaSerializerOptions(EndpointDefinition def, JsonSerializerOptions fallback)
        => def.SerializerContext?.Options is { } options
               ? EnsureTypeInfoResolver(options)
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

    static void EnrichInputSchemaWithValidation(JsonNode inputSchema, EndpointDefinition def, JsonSerializerOptions serializerOptions, IServiceProvider services)
    {
        if (def.ValidatorType is null)
            return;

        using var scope = services.CreateScope();

        if (TryResolveValidator(scope.ServiceProvider, def.ValidatorType) is { } validator)
            FluentValidationSchemaEnricher.Enrich(inputSchema, validator, def.ReqDtoType, serializerOptions);
    }

    static IValidator? TryResolveValidator(IServiceProvider services, Type validatorType)
    {
        return services.GetService(validatorType) as IValidator ?? (IValidator?)ActivatorUtilities.CreateInstance(services, validatorType);
    }

    static void RemoveNonClientInputProperties(JsonNode inputSchema, EndpointDefinition def, JsonSerializerOptions serializerOptions)
    {
        if (inputSchema is not JsonObject root || root["properties"] is not JsonObject props)
            return;

        var required = root["required"] as JsonArray;

        foreach (var prop in def.ReqDtoType.BindableProps())
        {
            if (!ShouldHideFromClientInput(prop))
                continue;

            foreach (var name in GetSchemaPropertyNameCandidates(prop, def.ReqDtoType, serializerOptions))
            {
                props.Remove(name);
                RemoveRequiredEntry(required, name);
            }
        }

        if (required is { Count: 0 })
            root.Remove("required");
    }

    static bool ShouldHideFromClientInput(PropertyInfo prop)
        => prop.GetCustomAttribute<HasPermissionAttribute>() is not null ||
           prop.GetCustomAttribute<FromClaimAttribute>() is { IsRequired: true } or { RemoveFromSchema: true };

    static IEnumerable<string> GetSchemaPropertyNameCandidates(PropertyInfo prop, Type dtoType, JsonSerializerOptions serializerOptions)
    {
        if (TryGetJsonPropertyName(prop, dtoType, serializerOptions) is { } jsonName)
            yield return jsonName;

        yield return prop.Name;
        yield return prop.FieldName();
    }

    static string? TryGetJsonPropertyName(PropertyInfo prop, Type dtoType, JsonSerializerOptions serializerOptions)
    {
        var typeInfo = serializerOptions.TypeInfoResolver?.GetTypeInfo(dtoType, serializerOptions);

        if (typeInfo is null)
            return null;

        foreach (var jsonProp in typeInfo.Properties)
        {
            if (ReferenceEquals(jsonProp.AttributeProvider, prop))
                return jsonProp.Name;
        }

        return null;
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

    sealed record ToolCatalog(IReadOnlyList<ToolRegistration> Tools, IReadOnlyDictionary<string, ToolRegistration[]> ToolsByName);
}
