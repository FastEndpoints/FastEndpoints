using System.Text.Json;
using FastEndpoints.Agents;

namespace FastEndpoints.A2A;

static class A2AMessageValidator
{
    const string DefaultMediaType = "application/json";
    const string RoleUser = "ROLE_USER";

    public static A2AMessage Validate(A2AMessage? message)
    {
        if (message is null)
            throw new A2ARpcException(JsonRpcError.InvalidParams("'message' is required."));

        if (string.IsNullOrWhiteSpace(message.MessageId))
            throw new A2ARpcException(JsonRpcError.InvalidParams("'message.messageId' is required."));

        if (message.Role is not RoleUser and not "user")
            throw new A2ARpcException(JsonRpcError.InvalidParams("'message.role' must be 'ROLE_USER'."));

        if (message.Parts is not { Length: > 0 } parts)
            throw new A2ARpcException(JsonRpcError.InvalidParams("'message.parts' must contain at least one part."));

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var hasData = part.Data is { ValueKind: not JsonValueKind.Undefined };
            var contentCount = (part.Text is not null ? 1 : 0) +
                               (hasData ? 1 : 0) +
                               (part.Raw is not null ? 1 : 0) +
                               (part.Url is not null ? 1 : 0);

            if (contentCount != 1)
                throw new A2ARpcException(JsonRpcError.InvalidParams($"'message.parts[{i}]' must contain exactly one of 'text', 'data', 'raw', or 'url'."));
        }

        return message;
    }

    public static void EnsureRequestedOutputModesAreSupported(string[]? acceptedOutputModes, A2ASkillInfo skill)
    {
        if (acceptedOutputModes is null)
            return;

        if (!acceptedOutputModes.Any(mode => GetOutputModes(skill).Contains(mode, StringComparer.OrdinalIgnoreCase)))
            throw new A2ARpcException(JsonRpcError.InvalidParams("no accepted output modes are supported by the requested skill."));
    }

    public static void EnsureActualOutputModeIsAccepted(InvocationResult result, string[]? acceptedOutputModes)
    {
        if (acceptedOutputModes is null)
            return;

        var mediaType = InvocationResultHelpers.NormalizeMediaType(result.ContentType, DefaultMediaType);

        if (!acceptedOutputModes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
            throw new A2ARpcException(JsonRpcError.InvalidParams($"response output mode '{mediaType}' is not accepted."));
    }

    public static void EnsureInputModesAreSupported(A2AMessage message, A2ASkillInfo skill)
    {
        var inputModes = GetInputModes(skill);

        foreach (var part in message.Parts!)
        {
            var mediaType = InvocationResultHelpers.NormalizeMediaType(part.MediaType, DefaultMediaType);

            if (!inputModes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
                throw new A2ARpcException(JsonRpcError.ContentTypeNotSupported(mediaType));
        }
    }

    static string[] GetOutputModes(A2ASkillInfo skill)
        => skill.OutputModes is { Length: > 0 } outputModes ? outputModes : [DefaultMediaType];

    static string[] GetInputModes(A2ASkillInfo skill)
        => skill.InputModes is { Length: > 0 } inputModes ? inputModes : [DefaultMediaType];
}
