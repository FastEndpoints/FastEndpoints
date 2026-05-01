using System.Text;
using System.Text.Json;
using FastEndpoints.Agents;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.A2A;

/// <summary>
/// dispatches A2A JSON-RPC <c>message/send</c> calls to the matching FastEndpoints endpoint. skill lookup
/// is by <c>A2ASkillInfo.Id</c>; the message <c>data</c> part is forwarded to <see cref="EndpointInvoker" />.
/// </summary>
sealed class A2ASkillDispatcher(IServiceProvider services, EndpointInvoker invoker, A2AOptions options)
{
    public async Task<object?> DispatchAsync(string method, JsonElement? parameters, HttpContext ctx, CancellationToken ct)
    {
        return method switch
        {
            "message/send" => await HandleMessageSend(parameters, ctx, ct),
            _ => throw new A2ARpcException(JsonRpcError.MethodNotFound(method))
        };
    }

    async Task<object?> HandleMessageSend(JsonElement? parameters, HttpContext ctx, CancellationToken ct)
    {
        if (parameters is null)
            throw new A2ARpcException(JsonRpcError.InvalidParams("'params' is required."));

        var serializerOptions = Config.SerOpts.Options;
        var p = parameters.Value.Deserialize<A2AMessageSendParams>(serializerOptions) ?? throw new A2ARpcException(JsonRpcError.InvalidParams("invalid 'params' payload."));

        if (string.IsNullOrWhiteSpace(p.Skill))
            throw new A2ARpcException(JsonRpcError.InvalidParams("'skill' is required."));

        var def = FindSkill(p.Skill, ctx) ?? throw new A2ARpcException(JsonRpcError.MethodNotFound($"skill '{p.Skill}'"));

        var args = ExtractArgs(p.Message, serializerOptions);
        var result = await invoker.InvokeAsync(def, args, ctx.User, ct);

        return result.Status switch
        {
            InvocationStatus.Success => BuildSuccess(result, serializerOptions),
            InvocationStatus.ValidationFailed => throw new A2ARpcException(
                                                     new()
                                                     {
                                                         Code = -32602,
                                                         Message = "validation failed",
                                                         Data = result.ValidationFailures.Select(f => new { f.PropertyName, f.ErrorMessage, f.ErrorCode })
                                                     }),
            InvocationStatus.Faulted => throw new A2ARpcException(JsonRpcError.Internal(result.Exception?.Message ?? "Endpoint invocation failed.")),
            _ => throw new A2ARpcException(JsonRpcError.Internal("Unknown invocation status."))
        };
    }

    static A2AMessage BuildSuccess(InvocationResult r, JsonSerializerOptions _)
    {
        var text = r.Body.Length == 0 ? string.Empty : Encoding.UTF8.GetString(r.Body);

        return new()
        {
            Role = "agent",
            Parts = [new() { Kind = "data", Text = text, MimeType = r.ContentType ?? "application/json" }]
        };
    }

    static JsonElement ExtractArgs(A2AMessage? message, JsonSerializerOptions _)
    {
        if (message?.Parts is { Length: > 0 } parts)
        {
            var dataPart = parts.FirstOrDefault(p => p.Kind == "data" && p.Data is not null);

            if (dataPart?.Data is { } data)
                return data;

            var textPart = parts.FirstOrDefault(p => p.Kind == "text" && !string.IsNullOrWhiteSpace(p.Text));

            if (textPart?.Text is { } text)
            {
                try { return JsonDocument.Parse(text).RootElement; }
                catch
                {
                    /* fall through to empty */
                }
            }
        }

        return JsonDocument.Parse("{}").RootElement;
    }

    EndpointDefinition? FindSkill(string id, HttpContext ctx)
    {
        var endpointData = services.GetRequiredService<EndpointData>();

        foreach (var def in endpointData.Found)
        {
            var info = def.ResolveSkillInfo();

            if (info is null)
                continue;

            if (options.SkillFilter is not null && !options.SkillFilter(def))
                continue;

            var summaryTitle = def.EndpointSummary?.Summary;
            var skillId =
                info.Id ?? (!string.IsNullOrWhiteSpace(summaryTitle) ? NamingHelpers.ToSnakeCase(summaryTitle) : null) ?? NamingHelpers.ToSnakeCase(def.EndpointType.Name);

            if (skillId == id)
            {
                if (options.SkillVisibilityFilter is not null && !options.SkillVisibilityFilter(def, ctx.User, ctx))
                    continue;

                return def;
            }
        }

        return null;
    }
}

sealed class A2ARpcException(JsonRpcError error) : Exception(error.Message)
{
    public JsonRpcError Error { get; } = error;
}
