namespace FastEndpoints;

/// <summary>
/// metadata captured by the core library when an endpoint is opted-in to be exposed as an
/// A2A (agent-to-agent) skill. this object is set on <see cref="EndpointDefinition.A2ASkill" />
/// either by the bare <c>A2ASkill(…)</c> call inside <see cref="BaseEndpoint.Configure" /> or by the
/// <c>[A2ASkill]</c> attribute. the <c>FastEndpoints.A2A</c> companion package reads it to build the
/// agent card entry and the JSON-RPC dispatcher for this skill.
/// </summary>
public sealed class A2ASkillInfo
{
    /// <summary>
    /// stable skill identifier used in the agent card and JSON-RPC dispatch. defaults to the
    /// endpoint type's short name in <c>snake_case</c> when <c>null</c>.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// short display name for this skill. defaults to <see cref="Id" /> when <c>null</c>.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// human-readable description of what the skill does. surfaces in the agent card and helps
    /// other agents decide whether to invoke this skill.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// free-form tags attached to the skill (e.g. <c>["orders","read"]</c>). discovered by remote
    /// agents to filter candidate skills.
    /// </summary>
    public string[]? Tags { get; set; }

    /// <summary>
    /// example natural-language prompts that should route to this skill. surfaces in the agent
    /// card under <c>examples</c> so that orchestrators can few-shot this skill effectively.
    /// </summary>
    public string[]? Examples { get; set; }

    /// <summary>
    /// MIME types the skill accepts as input. defaults to <c>["application/json"]</c> when <c>null</c>.
    /// </summary>
    public string[]? InputModes { get; set; }

    /// <summary>
    /// MIME types the skill produces as output. defaults to <c>["application/json"]</c> when <c>null</c>.
    /// </summary>
    public string[]? OutputModes { get; set; }
}
