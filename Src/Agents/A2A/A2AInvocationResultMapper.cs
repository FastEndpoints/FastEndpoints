using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using FastEndpoints.Agents;
using Microsoft.Extensions.Logging;

namespace FastEndpoints.A2A;

static class A2AInvocationResultMapper
{
    const string DefaultMediaType = "application/json";
    const string RoleAgent = "ROLE_AGENT";

    public static object Map(InvocationResult result, A2AMessage requestMessage, string[]? acceptedOutputModes, A2ASkillDescriptor skill, ILogger logger)
        => result.Status switch
        {
            InvocationStatus.Success => BuildSuccess(result, requestMessage, acceptedOutputModes),
            InvocationStatus.HttpError => throw new A2ARpcException(BuildHttpError(result)),
            InvocationStatus.ValidationFailed => throw new A2ARpcException(BuildValidationError(result)),
            InvocationStatus.Faulted => throw new A2ARpcException(BuildFaultedError(result, skill, logger)),
            _ => throw new A2ARpcException(JsonRpcError.Internal("Unknown invocation status."))
        };

    static A2ASendMessageResponse BuildSuccess(InvocationResult r, A2AMessage requestMessage, string[]? acceptedOutputModes)
    {
        A2AMessageValidator.EnsureActualOutputModeIsAccepted(r, acceptedOutputModes);

        var text = InvocationResultHelpers.ReadBodyText(r);

        return new()
        {
            Message = new()
            {
                MessageId = Guid.NewGuid().ToString("N"),
                ContextId = requestMessage.ContextId ?? Guid.NewGuid().ToString("N"),
                Role = RoleAgent,
                Parts = [BuildResponsePart(text, InvocationResultHelpers.NormalizeMediaType(r.ContentType))]
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
                contentType = InvocationResultHelpers.NormalizeMediaType(result.ContentType),
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

    static JsonRpcError BuildFaultedError(InvocationResult result, A2ASkillDescriptor skill, ILogger logger)
    {
        if (result.Exception is { } ex)
        {
            logger.LogError(
                ex,
                "A2A skill {SkillId} failed while invoking endpoint {EndpointType}.",
                skill.Id,
                skill.Definition.EndpointType.FullName ?? skill.Definition.EndpointType.Name);
        }

        return JsonRpcError.Internal("Endpoint invocation failed.");
    }

    static A2APart BuildResponsePart(string text, string mediaType)
    {
        if (IsJsonMediaType(mediaType) && InvocationResultHelpers.TryParseJson(text, out var data))
            return new() { Data = data, MediaType = mediaType };

        return new() { Text = text, MediaType = mediaType };
    }

    static bool IsJsonMediaType(string mediaType)
        => mediaType.Equals(DefaultMediaType, StringComparison.OrdinalIgnoreCase) || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    sealed class EndpointErrorData
    {
        public int statusCode { get; init; }
        public string contentType { get; init; } = DefaultMediaType;

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public object? body { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public string rawBody { get; init; } = string.Empty;
    }
}