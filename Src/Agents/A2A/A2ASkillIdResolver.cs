using FastEndpoints.Agents;

namespace FastEndpoints.A2A;

static class A2ASkillIdResolver
{
    internal static string ResolvePublishedId(EndpointDefinition def, A2ASkillInfo info)
        => AgentPublishedNameResolver.Resolve(def, info.Id, "A2A skill id", "A2A skill ids");
}
