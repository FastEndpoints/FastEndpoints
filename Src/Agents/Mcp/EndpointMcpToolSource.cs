using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints.Agents;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FastEndpoints.Mcp;

/// <summary>
/// builds <see cref="McpServerTool" /> instances for every FastEndpoints endpoint that opted-in via
/// <c>McpTool(...)</c> or <c>[McpTool]</c>. tools are registered into the MCP server's primary tool
/// collection at <c>MapFastEndpointsMcp</c> time; per-session filtering (auth visibility) is applied
/// inside each tool's invocation handler because tool visibility depends on <c>HttpContext.User</c>,
/// which is only available at call time.
/// </summary>
sealed class EndpointMcpToolSource
{
    readonly IServiceProvider _services;
    readonly McpOptions _options;

    public EndpointMcpToolSource(IServiceProvider services, McpOptions options)
    {
        _services = services;
        _options = options;
    }

    public IReadOnlyList<McpServerTool> BuildTools()
    {
        var endpointData = _services.GetRequiredService<EndpointData>();
        var serializerOptions = FastEndpoints.Config.SerOpts.Options;
        var invoker = _services.GetRequiredService<EndpointInvoker>();

        var tools = new List<McpServerTool>();
        foreach (var def in endpointData.Found)
        {
            if (def.McpToolInfo is null)
                continue;

            if (_options.ToolFilter is not null && !_options.ToolFilter(def))
                continue;

            tools.Add(BuildTool(def, invoker, serializerOptions));
        }
        return tools;
    }

    McpServerTool BuildTool(EndpointDefinition def, EndpointInvoker invoker, JsonSerializerOptions serializerOptions)
    {
        var info = def.McpToolInfo!;
        var name = info.Name ?? ToSnakeCase(def.EndpointType.Name);
        var description = info.Description ?? def.EndpointSummary?.Description;

        var inputSchema = JsonSchemaBuilder.Build(def.ReqDtoType, serializerOptions);
        if (TryResolveValidator(def) is { } validator)
            FluentValidationSchemaEnricher.Enrich(inputSchema, validator);

        JsonNode? outputSchema = null;
        if (_options.IncludeOutputSchemas && def.ResDtoType != typeof(object) && def.ResDtoType != typeof(void))
            outputSchema = JsonSchemaBuilder.Build(def.ResDtoType, serializerOptions);

        var protocolTool = new Tool
        {
            Name = name,
            Description = description,
            Title = info.Title,
            InputSchema = JsonSerializer.SerializeToElement(inputSchema, serializerOptions),
            OutputSchema = outputSchema is null ? null : JsonSerializer.SerializeToElement(outputSchema, serializerOptions),
            Annotations = BuildAnnotations(info)
        };

        return McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> ctx, CancellationToken ct) =>
            {
                var httpContext = ctx.Services?.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()?.HttpContext;
                var principal = httpContext?.User ?? ctx.User ?? new System.Security.Claims.ClaimsPrincipal();

                if (_options.ToolVisibilityFilter is not null &&
                    httpContext is not null &&
                    !_options.ToolVisibilityFilter(def, principal, httpContext))
                {
                    throw new McpException($"Tool '{name}' is not available for the current caller.");
                }

                var args = ctx.Params?.Arguments is { } argMap
                               ? JsonSerializer.SerializeToElement(argMap, serializerOptions)
                               : JsonDocument.Parse("{}").RootElement;

                var result = await invoker.InvokeAsync(def, args, principal, serializerOptions, ct);

                return result.Status switch
                {
                    InvocationStatus.Success => BuildSuccessResult(result),
                    InvocationStatus.ValidationFailed => BuildValidationErrorResult(result),
                    InvocationStatus.Faulted => throw new McpException(result.Exception?.Message ?? "Endpoint invocation failed.", result.Exception),
                    _ => throw new McpException("Unknown invocation status.")
                };
            },
            new McpServerToolCreateOptions { Name = name, Description = description, Title = info.Title });
    }

    static CallToolResult BuildSuccessResult(InvocationResult r)
    {
        var text = r.Body.Length == 0 ? string.Empty : Encoding.UTF8.GetString(r.Body);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = text }],
            IsError = false
        };
    }

    static CallToolResult BuildValidationErrorResult(InvocationResult r)
    {
        var errors = r.ValidationFailures.Select(f => new { property = f.PropertyName, error = f.ErrorMessage, code = f.ErrorCode });
        var json = JsonSerializer.Serialize(new { validationErrors = errors });
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = json }],
            IsError = true
        };
    }

    static ToolAnnotations? BuildAnnotations(McpToolInfo info)
    {
        var h = info.Hints;
        if (h.ReadOnly is null && h.Idempotent is null && h.Destructive is null && h.OpenWorld is null)
            return null;

        return new ToolAnnotations
        {
            ReadOnlyHint = h.ReadOnly,
            IdempotentHint = h.Idempotent,
            DestructiveHint = h.Destructive,
            OpenWorldHint = h.OpenWorld,
            Title = info.Title
        };
    }

    IValidator? TryResolveValidator(EndpointDefinition def)
    {
        if (def.ValidatorType is null)
            return null;

        return _services.GetService(def.ValidatorType) as IValidator
            ?? (IValidator?)ActivatorUtilities.CreateInstance(_services, def.ValidatorType);
    }

    static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new StringBuilder(input.Length + 8);
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(input[i - 1]))
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
