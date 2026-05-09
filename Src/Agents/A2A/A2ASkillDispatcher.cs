using System.Text.Json;
using FastEndpoints.Agents;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FastEndpoints.A2A;

/// <summary>
/// dispatches A2A v1 JSON-RPC <c>SendMessage</c> calls to the matching FastEndpoints endpoint.
/// if multiple visible skills exist, callers can select one with <c>params.metadata.skill</c>.
/// </summary>
sealed class A2ASkillDispatcher(A2ASkillCatalog skillCatalog, EndpointInvoker invoker, ILogger<A2ASkillDispatcher> logger)
{
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
        var p = DeserializeParams(parameters.Value, serializerOptions);

        var message = A2AMessageValidator.Validate(p.Message);

        var requestedSkill = A2AArgumentExtractor.GetRequestedSkill(p.Metadata);
        var skill = skillCatalog.FindVisibleSkill(requestedSkill, ctx) ?? throw new A2ARpcException(
                      requestedSkill is null
                           ? JsonRpcError.InvalidParams("multiple skills are available; set 'metadata.skill' to choose one.")
                           : JsonRpcError.MethodNotFound($"skill '{requestedSkill}'"));

        if (!string.IsNullOrWhiteSpace(message.TaskId))
            throw new A2ARpcException(JsonRpcError.TaskNotFound(message.TaskId));

        A2AMessageValidator.EnsureRequestedOutputModesAreSupported(p.Configuration?.AcceptedOutputModes, skill.Info);
        A2AMessageValidator.EnsureInputModesAreSupported(message, skill.Info);

        var args = A2AArgumentExtractor.Extract(message);
        var result = await invoker.InvokeAsync(skill.Definition, args, ctx.User, ct);

        return A2AInvocationResultMapper.Map(result, message, p.Configuration?.AcceptedOutputModes, skill, logger);
    }

    static A2AMessageSendParams DeserializeParams(JsonElement parameters, JsonSerializerOptions serializerOptions)
    {
        try
        {
            return parameters.Deserialize<A2AMessageSendParams>(serializerOptions) ?? throw new A2ARpcException(JsonRpcError.InvalidParams("invalid 'params' payload."));
        }
        catch (JsonException ex)
        {
            throw new A2ARpcException(JsonRpcError.InvalidParams($"invalid 'params' payload: {ex.Message}"));
        }
    }
}

sealed class A2ARpcException(JsonRpcError error) : Exception(error.Message)
{
    public JsonRpcError Error { get; } = error;
}
