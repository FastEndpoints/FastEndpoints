using System.Text.Json.Serialization;

namespace FastEndpoints.A2A;

/// <summary>
/// the agent card manifest served at <c>/.well-known/agent-card.json</c>. shape follows the
/// <a href="https://a2a-protocol.org/latest/specification/#agent-card">A2A specification</a>.
/// only the fields FastEndpoints can synthesise from opt-in endpoints are populated.
/// </summary>
public sealed class AgentCard
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("description")] public required string Description { get; init; }
    [JsonPropertyName("version")] public required string Version { get; init; }
    [JsonPropertyName("supportedInterfaces")] public required IReadOnlyList<AgentInterface> SupportedInterfaces { get; init; }
    [JsonPropertyName("provider")] public A2AProvider? Provider { get; init; }
    [JsonPropertyName("capabilities")] public AgentCapabilities Capabilities { get; init; } = new();
    [JsonPropertyName("defaultInputModes")] public string[] DefaultInputModes { get; init; } = ["application/json"];
    [JsonPropertyName("defaultOutputModes")] public string[] DefaultOutputModes { get; init; } = ["application/json"];
    [JsonPropertyName("skills")] public required IReadOnlyList<AgentSkill> Skills { get; init; }
}

public sealed class AgentInterface
{
    [JsonPropertyName("url")] public required string Url { get; init; }
    [JsonPropertyName("protocolBinding")] public required string ProtocolBinding { get; init; }
    [JsonPropertyName("tenant")] public string? Tenant { get; init; }
    [JsonPropertyName("protocolVersion")] public required string ProtocolVersion { get; init; }
}

public sealed class AgentCapabilities
{
    [JsonPropertyName("streaming")] public bool Streaming { get; init; }
    [JsonPropertyName("pushNotifications")] public bool PushNotifications { get; init; }
}

public sealed class AgentSkill
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("description")] public required string Description { get; init; }
    [JsonPropertyName("tags")] public string[] Tags { get; init; } = [];
    [JsonPropertyName("examples")] public string[]? Examples { get; init; }
    [JsonPropertyName("inputModes")] public string[]? InputModes { get; init; }
    [JsonPropertyName("outputModes")] public string[]? OutputModes { get; init; }
}
