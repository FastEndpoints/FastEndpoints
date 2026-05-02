using System.Security.Claims;
using System.Text;
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
    readonly Lazy<IReadOnlyList<ToolRegistration>> _tools = new(() => BuildRegistrations(services, options, logger));

    public IReadOnlyList<McpServerTool> BuildTools()
    {
        EnsureUniqueNames(_tools.Value, "registered MCP tools");

        return _tools.Value.Select(x => x.Tool).ToArray();
    }

    public IReadOnlyList<Tool> BuildVisibleProtocolTools(ClaimsPrincipal? user = null, HttpContext? httpContext = null)
    {
        var (principal, context) = ResolveCallerContext(user, httpContext);
        var visibleTools = _tools.Value.Where(x => options.ToolVisibilityFilter(x.Definition, principal, context)).ToArray();

        EnsureUniqueNames(visibleTools, "MCP tools visible to the current caller");

        return visibleTools.Select(x => x.Tool.ProtocolTool).ToArray();
    }

    public McpServerTool? ResolveVisibleTool(string name, ClaimsPrincipal? user = null, HttpContext? httpContext = null)
    {
        var (principal, context) = ResolveCallerContext(user, httpContext);
        var matches = _tools.Value.Where(x => x.Name == name && options.ToolVisibilityFilter(x.Definition, principal, context)).ToArray();

        return matches.Length switch
        {
            0 => null,
            1 => matches[0].Tool,
            _ => throw CreateDuplicateNameException(name, matches, "MCP tools visible to the current caller")
        };
    }

    (ClaimsPrincipal Principal, HttpContext HttpContext) ResolveCallerContext(ClaimsPrincipal? user, HttpContext? httpContext)
    {
        var principal = user ?? httpContext?.User ?? new ClaimsPrincipal();
        var resolvedHttpContext = httpContext ??
                                  services.GetService<IHttpContextAccessor>()?.HttpContext ??
                                  new DefaultHttpContext { RequestServices = services, User = principal };

        if (!ReferenceEquals(resolvedHttpContext.User, principal))
            resolvedHttpContext.User = principal;

        if (resolvedHttpContext.RequestServices == null!)
            resolvedHttpContext.RequestServices = services;

        return (principal, resolvedHttpContext);
    }

    static IReadOnlyList<ToolRegistration> BuildRegistrations(IServiceProvider services, McpOptions options, ILogger<EndpointMcpToolSource> logger)
    {
        var endpointData = services.GetRequiredService<EndpointData>();
        var serializerOptions = EnsureTypeInfoResolver(Config.SerOpts.Options);
        var invoker = services.GetRequiredService<EndpointInvoker>();

        var tools = new List<ToolRegistration>();

        foreach (var def in endpointData.Found)
        {
            var info = def.ResolveToolInfo();

            if (info is null)
                continue;

            if (options.ToolFilter is not null && !options.ToolFilter(def))
                continue;

            var tool = BuildTool(def, info, invoker, serializerOptions, services, options, logger);
            tools.Add(new(def, tool.ProtocolTool.Name, tool));
        }

        return tools;
    }

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
                    g =>
                        $"'{g.Key}' => {string.Join(", ", g.Select(x => x.Definition.EndpointType.FullName ?? x.Definition.EndpointType.Name).Distinct(StringComparer.Ordinal))}")) +
            ". MCP tool names must be unique.");
    }

    static InvalidOperationException CreateDuplicateNameException(string name, IReadOnlyCollection<ToolRegistration> matches, string scope)
        => new(
            $"Duplicate MCP tool name '{name}' detected among {scope}: " +
            string.Join(", ", matches.Select(x => x.Definition.EndpointType.FullName ?? x.Definition.EndpointType.Name).Distinct(StringComparer.Ordinal)) +
            ". MCP tool names must be unique.");

    static McpServerTool BuildTool(EndpointDefinition def,
                                   McpToolInfo info,
                                   EndpointInvoker invoker,
                                   JsonSerializerOptions serializerOptions,
                                   IServiceProvider services,
                                   McpOptions options,
                                   ILogger<EndpointMcpToolSource> logger)
    {
        var summaryTitle = def.EndpointSummary?.Summary;
        var name = info.Name ??
                   (!string.IsNullOrWhiteSpace(summaryTitle)
                        ? NamingHelpers.ToSnakeCase(summaryTitle)
                        : null) ??
                   NamingHelpers.ToSnakeCase(def.EndpointType.Name);
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

        var inputSchema = JsonSchemaBuilder.Build(def.ReqDtoType, serializerOptions);
        NormalizeRootObjectSchema(inputSchema);
        EnsureObjectRootSchema(inputSchema, name, def.ReqDtoType, "input", "arguments");
        if (TryResolveValidator(services, def) is { } validator)
            FluentValidationSchemaEnricher.Enrich(inputSchema, validator);

        JsonNode? outputSchema = null;

        if (options.IncludeOutputSchemas && def.ResDtoType != typeof(object) && def.ResDtoType != typeof(void))
        {
            var candidateOutputSchema = JsonSchemaBuilder.Build(def.ResDtoType, serializerOptions);
            NormalizeRootObjectSchema(candidateOutputSchema);

            if (HasObjectRootSchema(candidateOutputSchema))
                outputSchema = candidateOutputSchema;
        }

        var tool = McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> ctx, CancellationToken ct) =>
            {
                var requestUser = ctx.User ?? new ClaimsPrincipal();
                var httpContext = ctx.Services?.GetService<IHttpContextAccessor>()?.HttpContext ??
                                  new DefaultHttpContext { RequestServices = ctx.Services ?? services, User = requestUser };
                var principal = httpContext.User;

                if (!options.ToolVisibilityFilter(def, principal, httpContext))
                    throw new McpException($"Tool '{name}' is not available for the current caller.");

                var args = ctx.Params.Arguments is { } argMap
                               ? JsonSerializer.SerializeToElement(argMap, serializerOptions)
                               : JsonDocument.Parse("{}").RootElement;

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
                OutputSchema = outputSchema is null ? null : JsonSerializer.SerializeToElement(outputSchema, serializerOptions),
                SerializerOptions = serializerOptions
            });

        tool.ProtocolTool.InputSchema = JsonSerializer.SerializeToElement(inputSchema, serializerOptions);

        return tool;
    }

    static CallToolResult BuildSuccessResult(InvocationResult r, JsonNode? outputSchema)
    {
        var text = r.Body.Length == 0 ? string.Empty : Encoding.UTF8.GetString(r.Body);
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
        var errors = r.ValidationFailures.Select(f => new { property = f.PropertyName, error = f.ErrorMessage, code = f.ErrorCode });
        var json = JsonSerializer.Serialize(new { validationErrors = errors });

        return new()
        {
            Content = [new TextContentBlock { Text = json }],
            IsError = true
        };
    }

    static CallToolResult BuildHttpErrorResult(InvocationResult r)
    {
        var text = r.Body.Length == 0 ? string.Empty : Encoding.UTF8.GetString(r.Body);

        return new()
        {
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(new { statusCode = r.HttpStatusCode, contentType = r.ContentType, body = text }) }],
            IsError = true
        };
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

    static bool TryExtractStructuredContent(string text, out JsonElement structuredContent)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            structuredContent = default;

            return false;
        }

        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(text);

            if (json.ValueKind == JsonValueKind.Object)
            {
                structuredContent = json;

                return true;
            }
        }
        catch (JsonException)
        {
            // non-JSON or non-object payloads keep the existing text-only result
        }

        structuredContent = default;

        return false;
    }

    static IValidator? TryResolveValidator(IServiceProvider services, EndpointDefinition def)
    {
        if (def.ValidatorType is null)
            return null;

        return services.GetService(def.ValidatorType) as IValidator ?? (IValidator?)ActivatorUtilities.CreateInstance(services, def.ValidatorType);
    }

    sealed record ToolRegistration(EndpointDefinition Definition, string Name, McpServerTool Tool);
}
