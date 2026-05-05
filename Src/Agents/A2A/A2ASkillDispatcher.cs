using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints.Agents;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints.A2A;

/// <summary>
/// dispatches A2A v1 JSON-RPC <c>SendMessage</c> calls to the matching FastEndpoints endpoint.
/// if multiple visible skills exist, callers can select one with <c>params.metadata.skill</c>.
/// </summary>
sealed class A2ASkillDispatcher(A2ASkillCatalog skillCatalog, EndpointInvoker invoker)
{
    const string DefaultMediaType = "application/json";

    public async Task<object?> DispatchAsync(string method, JsonElement? parameters, HttpContext ctx, CancellationToken ct)
    {
        return method switch
        {
            "SendMessage" => await HandleMessageSend(parameters, ctx, ct),
            _ => throw new A2ARpcException(JsonRpcError.MethodNotFound(method))
        };
    }

    async Task<object?> HandleMessageSend(JsonElement? parameters, HttpContext ctx, CancellationToken ct)
    {
        if (parameters is null)
            throw new A2ARpcException(JsonRpcError.InvalidParams("'params' is required."));

        var serializerOptions = Config.SerOpts.Options;
        var p = parameters.Value.Deserialize<A2AMessageSendParams>(serializerOptions) ?? throw new A2ARpcException(JsonRpcError.InvalidParams("invalid 'params' payload."));

        var message = ValidateMessage(p.Message);

        var requestedSkill = GetRequestedSkill(p.Metadata);
        var def = skillCatalog.FindVisibleSkill(requestedSkill, ctx) ?? throw new A2ARpcException(
                      requestedSkill is null
                           ? JsonRpcError.InvalidParams("multiple skills are available; set 'metadata.skill' to choose one.")
                           : JsonRpcError.MethodNotFound($"skill '{requestedSkill}'"));

        var args = ExtractArgs(message);
        var result = await invoker.InvokeAsync(def, args, ctx.User, ct);

        return result.Status switch
        {
            InvocationStatus.Success => BuildSuccess(result, message),
            InvocationStatus.HttpError => throw new A2ARpcException(BuildHttpError(result)),
            InvocationStatus.ValidationFailed => throw new A2ARpcException(BuildValidationError(result)),
            InvocationStatus.Faulted => throw new A2ARpcException(JsonRpcError.Internal(result.Exception?.Message ?? "Endpoint invocation failed.")),
            _ => throw new A2ARpcException(JsonRpcError.Internal("Unknown invocation status."))
        };
    }

    static A2ASendMessageResponse BuildSuccess(InvocationResult r, A2AMessage requestMessage)
    {
        var text = InvocationResultHelpers.ReadBodyText(r);

        return new()
        {
            Message = new()
            {
                MessageId = Guid.NewGuid().ToString("N"),
                ContextId = requestMessage.ContextId,
                TaskId = requestMessage.TaskId,
                Role = "ROLE_AGENT",
                Parts = [BuildResponsePart(text, InvocationResultHelpers.NormalizeMediaType(r.ContentType, DefaultMediaType))]
            }
        };
    }

    static JsonRpcError BuildHttpError(InvocationResult result)
    {
        var text = InvocationResultHelpers.ReadBodyText(result);
        object? body = InvocationResultHelpers.TryParseJson(text, out var json) ? json : null;

        return new()
        {
            Code = -32000,
            Message = $"Endpoint returned HTTP {result.HttpStatusCode}.",
            Data = new EndpointErrorData
            {
                statusCode = result.HttpStatusCode,
                contentType = InvocationResultHelpers.NormalizeMediaType(result.ContentType, DefaultMediaType),
                body = body,
                rawBody = text
            }
        };
    }

    static JsonRpcError BuildValidationError(InvocationResult result)
        => new()
        {
            Code = -32602,
            Message = "validation failed",
            Data = result.ValidationFailures.Select(f => new { f.PropertyName, f.ErrorMessage, f.ErrorCode })
        };

    static A2APart BuildResponsePart(string text, string mediaType)
    {
        if (InvocationResultHelpers.TryParseJson(text, out var data))
            return new() { Data = data, MediaType = mediaType };

        return new() { Text = text, MediaType = mediaType };
    }

    sealed class EndpointErrorData
    {
        public int statusCode { get; init; }
        public string contentType { get; init; } = DefaultMediaType;

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public object? body { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string rawBody { get; init; } = string.Empty;
    }

    static A2AMessage ValidateMessage(A2AMessage? message)
    {
        if (message is null)
            throw new A2ARpcException(JsonRpcError.InvalidParams("'message' is required."));

        if (string.IsNullOrWhiteSpace(message.MessageId))
            throw new A2ARpcException(JsonRpcError.InvalidParams("'message.messageId' is required."));

        if (message.Parts is not { Length: > 0 } parts)
            throw new A2ARpcException(JsonRpcError.InvalidParams("'message.parts' must contain at least one part."));

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var hasData = part.Data is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined };
            var contentCount = (part.Text is not null ? 1 : 0) +
                               (hasData ? 1 : 0) +
                               (part.Raw is not null ? 1 : 0) +
                               (part.Url is not null ? 1 : 0);

            if (contentCount != 1)
                throw new A2ARpcException(JsonRpcError.InvalidParams($"'message.parts[{i}]' must contain exactly one of 'text', 'data', 'raw', or 'url'."));

            if (hasData && part.Data!.Value.ValueKind != JsonValueKind.Object)
                throw new A2ARpcException(JsonRpcError.InvalidParams($"'message.parts[{i}].data' must be a JSON object."));
        }

        return message;
    }

    static JsonElement ExtractArgs(A2AMessage message)
    {
        foreach (var part in message.Parts!)
        {
            if (part.Data is { ValueKind: JsonValueKind.Object } data)
                return data;

            if (part.Text is { } text)
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);

                    return doc.RootElement.Clone();
                }
                catch (JsonException)
                {
                    throw new A2ARpcException(JsonRpcError.InvalidParams("text parts must contain valid JSON to invoke a skill."));
                }
            }
        }

        throw new A2ARpcException(JsonRpcError.InvalidParams("no supported input part found. only 'data' and JSON 'text' parts can invoke skills."));
    }

    static string? GetRequestedSkill(JsonElement? metadata)
    {
        if (metadata is null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
            return null;

        return metadata.Value.ValueKind == JsonValueKind.Object &&
               metadata.Value.TryGetProperty("skill", out var skill) &&
               skill.ValueKind == JsonValueKind.String
                   ? skill.GetString()
                   : null;
    }

}

sealed class A2ARpcException(JsonRpcError error) : Exception(error.Message)
{
    public JsonRpcError Error { get; } = error;
}
