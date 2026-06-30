using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints.Agents;
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
        => CallerContextResolver.Resolve(services, user, httpContext);

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

                var tool = BuildTool(
                    def,
                    info,
                    invoker,
                    McpToolSchemaFactory.ResolveSerializerOptions(def, protocolSerializerOptions),
                    protocolSerializerOptions,
                    services,
                    options,
                    logger);

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
        var name = AgentPublishedNameResolver.Resolve(def, info.Name, "MCP tool name", "MCP tool names");
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

        var inputSchema = McpToolSchemaFactory.BuildInputSchema(def, schemaSerializerOptions, name, services);
        var outputSchema = McpToolSchemaFactory.TryBuildOutputSchema(def, schemaSerializerOptions, options);
        var outputPropertyNames = GetRootPropertyNames(outputSchema);

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
                    InvocationStatus.Success => BuildSuccessResult(result, outputSchema, outputPropertyNames),
                    InvocationStatus.HttpError => BuildHttpErrorResult(result),
                    InvocationStatus.ValidationFailed => BuildValidationErrorResult(result),
                    InvocationStatus.Faulted => BuildFaultedResult(result, logger, name, def),
                    _ => throw new McpException("Unknown invocation status.")
                };
            },
            new()
            {
                Name = name.Replace('/', '_'),
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

        // The current MCP SDK rejects forward slashes during tool construction even though the spec allows them.
        // Publish the resolved protocol name after construction so FastEndpoints can follow the spec.
        tool.ProtocolTool.Name = name;
        tool.ProtocolTool.InputSchema = JsonSerializer.SerializeToElement(inputSchema, protocolSerializerOptions);

        return tool;
    }

    static CallToolResult BuildSuccessResult(InvocationResult r, JsonNode? outputSchema, IReadOnlySet<string>? outputPropertyNames)
    {
        var text = InvocationResultHelpers.ReadBodyText(r);
        JsonElement? structuredContent = null;

        if (outputSchema is not null && McpStructuredContentMapper.TryMap(text, outputSchema, outputPropertyNames, out var schemaContent))
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

    static IReadOnlySet<string>? GetRootPropertyNames(JsonNode? schema)
        => schema is JsonObject { } root && root["properties"] is JsonObject props
               ? props.Select(p => p.Key).ToHashSet(StringComparer.Ordinal)
               : null;

    sealed record ToolRegistration(EndpointDefinition Definition, string Name, McpServerTool Tool);
}