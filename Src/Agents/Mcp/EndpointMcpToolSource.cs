using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints.Agents;
using FluentValidation;
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
    public IReadOnlyList<McpServerTool> BuildTools()
    {
        var endpointData = services.GetRequiredService<EndpointData>();
        var serializerOptions = Config.SerOpts.Options;
        var invoker = services.GetRequiredService<EndpointInvoker>();

        var tools = new List<McpServerTool>();

        foreach (var def in endpointData.Found)
        {
            var info = def.ResolveToolInfo();

            if (info is null)
                continue;

            if (options.ToolFilter is not null && !options.ToolFilter(def))
                continue;

            tools.Add(BuildTool(def, info, invoker, serializerOptions));
        }

        return tools;
    }

    McpServerTool BuildTool(EndpointDefinition def, McpToolInfo info, EndpointInvoker invoker, JsonSerializerOptions serializerOptions)
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
        if (TryResolveValidator(def) is { } validator)
            FluentValidationSchemaEnricher.Enrich(inputSchema, validator);

        if (options.IncludeOutputSchemas && def.ResDtoType != typeof(object) && def.ResDtoType != typeof(void))
        {
            var outputSchema = JsonSchemaBuilder.Build(def.ResDtoType, serializerOptions);
            NormalizeRootObjectSchema(outputSchema);
        }

        return McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> ctx, CancellationToken ct) =>
            {
                var requestUser = ctx.User ?? new System.Security.Claims.ClaimsPrincipal();
                var httpContext = ctx.Services?.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()?.HttpContext ??
                                  new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = ctx.Services ?? services, User = requestUser };
                var principal = httpContext.User;

                if (!options.ToolVisibilityFilter(def, principal, httpContext))
                    throw new McpException($"Tool '{name}' is not available for the current caller.");

                var args = ctx.Params.Arguments is { } argMap
                               ? JsonSerializer.SerializeToElement(argMap, serializerOptions)
                               : JsonDocument.Parse("{}").RootElement;

                var result = await invoker.InvokeAsync(def, args, principal, ct);

                return result.Status switch
                {
                    InvocationStatus.Success => BuildSuccessResult(result),
                    InvocationStatus.HttpError => BuildHttpErrorResult(result),
                    InvocationStatus.ValidationFailed => BuildValidationErrorResult(result),
                    InvocationStatus.Faulted => throw new McpException(result.Exception?.Message ?? "Endpoint invocation failed.", result.Exception),
                    _ => throw new McpException("Unknown invocation status.")
                };
            },
            new() { Name = name, Description = description, Title = info.Title });
    }

    static CallToolResult BuildSuccessResult(InvocationResult r)
    {
        var text = r.Body.Length == 0 ? string.Empty : Encoding.UTF8.GetString(r.Body);

        return new()
        {
            Content = [new TextContentBlock { Text = text }],
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

    IValidator? TryResolveValidator(EndpointDefinition def)
    {
        if (def.ValidatorType is null)
            return null;

        return services.GetService(def.ValidatorType) as IValidator ?? (IValidator?)ActivatorUtilities.CreateInstance(services, def.ValidatorType);
    }
}