namespace FastEndpoints.A2A;

/// <summary>
/// opt an endpoint in to being exposed as an A2A (agent-to-agent) skill. the
/// <c>FastEndpoints.A2A</c> addon scans the endpoint's public
/// <see cref="EndpointDefinition.EndpointAttributes" /> for this attribute at <c>UseA2A()</c> time.
/// use this form on attribute-configured endpoints; endpoints that override <c>Configure()</c>
/// should call <c>A2AEndpointExtensions.A2ASkill(...)</c>
/// instead. core FastEndpoints has no knowledge of this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class A2ASkillAttribute : Attribute
{
    /// <summary>
    /// stable skill identifier. <c>null</c> uses the endpoint type name converted to <c>snake_case</c>.
    /// </summary>
    public string? Id { get; }

    /// <summary>
    /// short display name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// human-readable description of what this skill does.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// tags other agents can filter by when selecting skills.
    /// </summary>
    public string[]? Tags { get; set; }

    /// <summary>
    /// example natural-language prompts that should route to this skill.
    /// </summary>
    public string[]? Examples { get; set; }

    /// <summary>
    /// accepted input MIME types. defaults to <c>["application/json"]</c> when unset.
    /// </summary>
    public string[]? InputModes { get; set; }

    /// <summary>
    /// produced output MIME types. defaults to <c>["application/json"]</c> when unset.
    /// </summary>
    public string[]? OutputModes { get; set; }

    /// <inheritdoc cref="A2ASkillAttribute" />
    public A2ASkillAttribute(string? id = null)
    {
        Id = id;
    }

    internal A2ASkillInfo ToInfo()
        => new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Tags = Tags,
            Examples = Examples,
            InputModes = InputModes,
            OutputModes = OutputModes
        };
}
