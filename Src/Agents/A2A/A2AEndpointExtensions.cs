namespace FastEndpoints.A2A;

/// <summary>
/// opt-in extension methods for exposing FastEndpoints endpoints as A2A skills. add
/// <c>using FastEndpoints.A2A;</c> to your endpoint file and call <c>this.A2ASkill(...)</c>
/// inside <c>Configure()</c>. the <c>this.</c> prefix is required by the C# language for an
/// extension method call on the enclosing instance — it is not a style preference, bare
/// <c>A2ASkill(...)</c> will not resolve. the alternative, <c>Definition.A2ASkill(...)</c>, is
/// useful when composing configuration from helpers that only see the endpoint definition.
/// <para>
/// the addon stores a single <see cref="A2ASkillInfo" /> instance on the endpoint's public
/// <see cref="EndpointDefinition.EndpointMetadata" /> bag — no modification to the core library
/// is needed.
/// </para>
/// </summary>
public static class A2AEndpointExtensions
{
    /// <summary>
    /// opt this endpoint in to being exposed as an A2A (agent-to-agent) skill via the
    /// <c>FastEndpoints.A2A</c> addon. without an opt-in call the endpoint is invisible to A2A
    /// clients — the safe default.
    /// </summary>
    /// <param name="ep">the endpoint (this parameter). call as <c>this.A2ASkill(...)</c> inside <c>Configure()</c>.</param>
    /// <param name="id">stable skill identifier. <c>null</c> uses the endpoint type name in <c>snake_case</c>.</param>
    /// <param name="tags">free-form tags other agents can filter by when selecting skills.</param>
    /// <param name="configure">optional callback for setting name, description, examples, and input/output modes.</param>
    public static void A2ASkill(this BaseEndpoint ep, string? id = null, string[]? tags = null, Action<A2ASkillInfo>? configure = null)
        => ep.Definition.A2ASkill(id, tags, configure);

    extension(EndpointDefinition def)
    {
        /// <summary>
        /// opt this endpoint in to being exposed as an A2A skill. identical to the <see cref="BaseEndpoint" />
        /// overload but targets the <see cref="EndpointDefinition" /> directly — useful when composing
        /// configuration from helpers that only see the definition.
        /// </summary>
        public void A2ASkill(string? id = null, string[]? tags = null, Action<A2ASkillInfo>? configure = null)
        {
            var info = GetMetadataSkillInfo(def) ?? CreateSkillInfo(def);

            if (id is not null)
                info.Id = id;

            if (tags is not null)
                info.Tags = tags;

            configure?.Invoke(info);
        }

        /// <summary>
        /// resolve the <see cref="A2ASkillInfo" /> attached to an endpoint, preferring a fluent
        /// <c>A2ASkill(...)</c> registration (stored on <see cref="EndpointDefinition.EndpointMetadata" />)
        /// and falling back to an <see cref="A2ASkillAttribute" /> captured in
        /// <see cref="EndpointDefinition.EndpointAttributes" />. returns <c>null</c> when the endpoint
        /// has not opted in.
        /// </summary>
        internal A2ASkillInfo? ResolveSkillInfo()
        {
            var info = GetMetadataSkillInfo(def);

            if (info is not null)
                return info;

            if (def.EndpointAttributes is not { } attrs)
                return null;

            foreach (var a in attrs)
            {
                if (a is A2ASkillAttribute attr)
                    return attr.ToInfo();
            }

            return null;
        }
    }

    static A2ASkillInfo? GetMetadataSkillInfo(EndpointDefinition def)
    {
        if (def.EndpointMetadata is { } meta)
        {
            foreach (var o in meta)
            {
                if (o is A2ASkillInfo existing)
                    return existing;
            }
        }

        return null;
    }

    static A2ASkillInfo CreateSkillInfo(EndpointDefinition def)
    {
        var info = new A2ASkillInfo();
        def.Metadata(info);

        return info;
    }
}
